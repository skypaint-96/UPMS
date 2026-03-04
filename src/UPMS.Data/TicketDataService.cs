namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;
using System.Globalization;

/// <summary>
/// EF Core–backed service providing ticket and snapshot data operations.
/// Replaces the legacy static Dapper-based implementation.
/// </summary>
public class TicketDataService
{
    private readonly UpmsDbContext _context;

    public TicketDataService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    private static DateTime ParseDateTime(object? value)
    {
        if (value is DateTime dt) return dt;
        if (value is DateTimeOffset dto) return dto.UtcDateTime;
        if (value is string s && !string.IsNullOrEmpty(s))
        {
            if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var r1))
                return r1;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var r2))
                return r2;
        }
        return DateTime.MinValue;
    }

    /// <summary>Creates a new snapshot for an ITSM source.</summary>
    public async Task<Guid> CreateSnapshotAsync(
        string itsmSource,
        DateTime snapshotDate,
        string uploadedBy = "system",
        string? uploadMetadata = null)
    {
        if (string.IsNullOrWhiteSpace(itsmSource))
            throw new ArgumentException("ITSM source is required.", nameof(itsmSource));
        if (snapshotDate == default)
            throw new ArgumentException("Snapshot date is required.", nameof(snapshotDate));

        var snapshot = new Snapshot
        {
            Id = Guid.NewGuid(),
            ItsmSource = itsmSource,
            SnapshotDate = snapshotDate,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            UploadMetadata = uploadMetadata
        };

        _context.Snapshots.Add(snapshot);
        await _context.SaveChangesAsync();
        return snapshot.Id;
    }

    /// <summary>Adds tickets to a snapshot with their associated company names.</summary>
    public async Task AddTicketsToSnapshotAsync(
        Guid snapshotId,
        IEnumerable<(string TicketKey, string CompanyName)> ticketsWithCompanies)
    {
        if (snapshotId == Guid.Empty)
            throw new ArgumentException("Snapshot ID cannot be empty.", nameof(snapshotId));
        ArgumentNullException.ThrowIfNull(ticketsWithCompanies);

        var validTickets = ticketsWithCompanies
            .Where(t => !string.IsNullOrWhiteSpace(t.TicketKey) && !string.IsNullOrWhiteSpace(t.CompanyName))
            .Select(t => (t.TicketKey.Trim(), t.CompanyName.Trim()))
            .Distinct()
            .ToList();

        if (validTickets.Count == 0)
            return;

        // Use a single bulk INSERT with ON CONFLICT DO NOTHING via array unnesting
        var ids = validTickets.Select(_ => Guid.NewGuid()).ToArray();
        var snapshotIds = validTickets.Select(_ => snapshotId).ToArray();
        var companies = validTickets.Select(t => t.Item2).ToArray();
        var ticketKeys = validTickets.Select(t => t.Item1).ToArray();

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO snapshot_ticket (id, snapshot_id, company_name, ticket_key)
            SELECT * FROM unnest({ids}::uuid[], {snapshotIds}::uuid[], {companies}::varchar[], {ticketKeys}::varchar[])
                AS t(id, snapshot_id, company_name, ticket_key)
            ON CONFLICT (snapshot_id, ticket_key) DO NOTHING
            """);
    }

    /// <summary>Records a field change for a ticket.</summary>
    public async Task RecordFieldChangeAsync(
        string companyName,
        string ticketKey,
        string fieldName,
        string? fieldValue,
        DateTime observedAt,
        Guid snapshotId)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));
        if (string.IsNullOrWhiteSpace(ticketKey))
            throw new ArgumentException("Ticket key is required.", nameof(ticketKey));
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name is required.", nameof(fieldName));
        if (snapshotId == Guid.Empty)
            throw new ArgumentException("Snapshot ID cannot be empty.", nameof(snapshotId));
        if (observedAt == default)
            throw new ArgumentException("Observed time is required.", nameof(observedAt));

        var change = new FieldChange
        {
            CompanyName = companyName,
            TicketKey = ticketKey,
            FieldName = fieldName,
            FieldValue = fieldValue,
            ObservedAt = observedAt,
            SnapshotId = snapshotId
        };

        _context.FieldChanges.Add(change);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Retrieves tickets for a company from a specific ITSM source as they appeared at a specific point in time.
    /// Uses a single bulk field fetch to avoid N+1 queries.
    /// </summary>
    public async Task<IEnumerable<Ticket>> GetTicketsAsync(string itsmSource, string companyName, DateTime asOfDate)
    {
        if (string.IsNullOrWhiteSpace(itsmSource))
            throw new ArgumentException("ITSM source is required.", nameof(itsmSource));
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));

        // Step 1: Find all snapshots for this source up to asOfDate
        var snapshotIds = await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.ItsmSource == itsmSource && s.SnapshotDate <= asOfDate)
            .Select(s => new { s.Id, s.SnapshotDate })
            .ToListAsync();

        if (snapshotIds.Count == 0)
            return [];

        var snapshotIdSet = snapshotIds.Select(s => s.Id).ToHashSet();
        var snapshotDateLookup = snapshotIds.ToDictionary(s => s.Id, s => s.SnapshotDate);

        // Step 2: Find latest snapshot per ticket key for this company
        var ticketRows = await _context.SnapshotTickets
            .AsNoTracking()
            .Where(st => snapshotIdSet.Contains(st.SnapshotId) && st.CompanyName == companyName)
            .GroupBy(st => st.TicketKey)
            .Select(g => new
            {
                TicketKey = g.Key,
                SnapshotId = g.OrderByDescending(x => snapshotDateLookup.ContainsKey(x.SnapshotId) ? snapshotDateLookup[x.SnapshotId] : DateTime.MinValue)
                              .Select(x => x.SnapshotId)
                              .First()
            })
            .ToListAsync();

        if (ticketRows.Count == 0)
            return [];

        var ticketKeys = ticketRows.Select(t => t.TicketKey).ToList();

        // Step 3: Bulk fetch all field values for all tickets at once (fixes N+1)
        var allFields = await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.CompanyName == companyName
                      && ticketKeys.Contains(fc.TicketKey)
                      && fc.ObservedAt <= asOfDate)
            .GroupBy(fc => new { fc.TicketKey, fc.FieldName })
            .Select(g => new
            {
                g.Key.TicketKey,
                g.Key.FieldName,
                FieldValue = g.OrderByDescending(x => x.ObservedAt).Select(x => x.FieldValue).First(),
                ObservedAt = g.Max(x => x.ObservedAt)
            })
            .ToListAsync();

        // Group fields by ticket key
        var fieldsByTicket = allFields
            .GroupBy(f => f.TicketKey)
            .ToDictionary(
                g => g.Key,
                g => (IDictionary<string, string?>)g.ToDictionary(f => f.FieldName, f => f.FieldValue));

        var maxObservedByTicket = allFields
            .GroupBy(f => f.TicketKey)
            .ToDictionary(g => g.Key, g => g.Max(f => f.ObservedAt));

        return ticketRows.Select(row =>
        {
            var snapshotDate = snapshotDateLookup.TryGetValue(row.SnapshotId, out var sd) ? sd : DateTime.MinValue;
            return new Ticket
            {
                TicketKey = row.TicketKey,
                CompanyName = companyName,
                ItsmSource = itsmSource,
                Fields = fieldsByTicket.TryGetValue(row.TicketKey, out var fields) ? fields : new Dictionary<string, string?>(),
                ObservedAt = maxObservedByTicket.TryGetValue(row.TicketKey, out var obs) ? obs : DateTime.MinValue,
                SnapshotId = row.SnapshotId,
                SnapshotDate = snapshotDate
            };
        }).ToList();
    }

    /// <summary>
    /// Retrieves all tickets from a specific snapshot.
    /// Uses a single bulk field fetch to avoid N+1 queries.
    /// </summary>
    public async Task<IEnumerable<Ticket>> GetTicketsBySnapshotAsync(Guid snapshotId)
    {
        var snapshot = await _context.Snapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == snapshotId);

        if (snapshot is null)
            return [];

        var ticketRows = await _context.SnapshotTickets
            .AsNoTracking()
            .Where(st => st.SnapshotId == snapshotId)
            .OrderBy(st => st.TicketKey)
            .Select(st => new { st.TicketKey, st.CompanyName })
            .ToListAsync();

        if (ticketRows.Count == 0)
            return [];

        // Group by company for efficient field lookup
        var companiesAndKeys = ticketRows
            .Select(t => new { t.CompanyName, t.TicketKey })
            .ToList();

        var ticketKeys = ticketRows.Select(t => t.TicketKey).Distinct().ToList();
        var companies = ticketRows.Select(t => t.CompanyName).Distinct().ToList();

        // Bulk fetch all field values for all tickets at once (fixes N+1)
        var allFields = await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.SnapshotId == snapshotId
                      && ticketKeys.Contains(fc.TicketKey)
                      && companies.Contains(fc.CompanyName))
            .GroupBy(fc => new { fc.CompanyName, fc.TicketKey, fc.FieldName })
            .Select(g => new
            {
                g.Key.CompanyName,
                g.Key.TicketKey,
                g.Key.FieldName,
                FieldValue = g.OrderByDescending(x => x.ObservedAt).Select(x => x.FieldValue).First(),
                ObservedAt = g.Max(x => x.ObservedAt)
            })
            .ToListAsync();

        // Group fields by (company, ticketKey)
        var fieldsByCompanyTicket = allFields
            .GroupBy(f => (f.CompanyName, f.TicketKey))
            .ToDictionary(
                g => g.Key,
                g => (IDictionary<string, string?>)g.ToDictionary(f => f.FieldName, f => f.FieldValue));

        var maxObservedByCompanyTicket = allFields
            .GroupBy(f => (f.CompanyName, f.TicketKey))
            .ToDictionary(g => g.Key, g => g.Max(f => f.ObservedAt));

        return ticketRows.Select(row =>
        {
            var key = (row.CompanyName, row.TicketKey);
            return new Ticket
            {
                TicketKey = row.TicketKey,
                CompanyName = row.CompanyName,
                ItsmSource = snapshot.ItsmSource,
                Fields = fieldsByCompanyTicket.TryGetValue(key, out var fields) ? fields : new Dictionary<string, string?>(),
                ObservedAt = maxObservedByCompanyTicket.TryGetValue(key, out var obs) ? obs : DateTime.MinValue,
                SnapshotId = snapshotId,
                SnapshotDate = snapshot.SnapshotDate
            };
        }).ToList();
    }

    /// <summary>Retrieves the complete change history for a specific field of a ticket.</summary>
    public async Task<IEnumerable<FieldChange>> GetTicketFieldHistoryAsync(
        string companyName,
        string ticketKey,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));

        return await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.CompanyName == companyName
                      && fc.TicketKey == ticketKey
                      && fc.FieldName == fieldName)
            .OrderBy(fc => fc.ObservedAt)
            .ToListAsync();
    }

    /// <summary>Retrieves all snapshots, optionally filtered by ITSM source.</summary>
    public async Task<IEnumerable<Snapshot>> GetSnapshotsAsync(string? itsmSource = null)
    {
        var query = _context.Snapshots.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(itsmSource))
            query = query.Where(s => s.ItsmSource == itsmSource);

        return await query.OrderByDescending(s => s.SnapshotDate).ToListAsync();
    }

    /// <summary>Retrieves snapshots filtered by optional ITSM source and/or company name.</summary>
    public async Task<IEnumerable<Snapshot>> GetSnapshotsAsync(string? itsmSource, string? companyName)
    {
        var query = _context.Snapshots.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(itsmSource))
            query = query.Where(s => s.ItsmSource == itsmSource);

        if (!string.IsNullOrWhiteSpace(companyName))
        {
            var snapshotIdsWithCompany = _context.SnapshotTickets
                .AsNoTracking()
                .Where(st => st.CompanyName == companyName)
                .Select(st => st.SnapshotId)
                .Distinct();

            query = query.Where(s => snapshotIdsWithCompany.Contains(s.Id));
        }

        return await query.OrderByDescending(s => s.SnapshotDate).ToListAsync();
    }
}
