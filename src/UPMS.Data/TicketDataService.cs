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

    private static DateTime NormalizeToUtc(DateTime value)
    {
        if (value == default) return value;
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime()
        };
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

        // Normalize and de-duplicate input. The DB enforces uniqueness on (snapshot_id, ticket_key),
        // so we group by ticket key and keep the first company name we see for that key.
        var normalized = ticketsWithCompanies
            .Where(t => !string.IsNullOrWhiteSpace(t.TicketKey) && !string.IsNullOrWhiteSpace(t.CompanyName))
            .Select(t => (TicketKey: t.TicketKey.Trim(), CompanyName: t.CompanyName.Trim()))
            .GroupBy(t => t.TicketKey)
            .Select(g => g.First())
            .ToList();

        if (normalized.Count == 0)
            return;

        var keys = normalized.Select(t => t.TicketKey).Distinct().ToList();

        // Idempotent behavior: skip keys that already exist for this snapshot.
        var existingKeys = await _context.SnapshotTickets
            .AsNoTracking()
            .Where(st => st.SnapshotId == snapshotId && keys.Contains(st.TicketKey))
            .Select(st => st.TicketKey)
            .ToListAsync();

        var existingSet = existingKeys.ToHashSet();

        var newSnapshotTickets = normalized
            .Where(t => !existingSet.Contains(t.TicketKey))
            .Select(t => new SnapshotTicket
            {
                Id = Guid.NewGuid(),
                SnapshotId = snapshotId,
                CompanyName = t.CompanyName,
                TicketKey = t.TicketKey
            })
            .ToList();

        if (newSnapshotTickets.Count == 0)
            return;

        _context.SnapshotTickets.AddRange(newSnapshotTickets);
        await _context.SaveChangesAsync();
    }

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

        DateTime asOfUtc = NormalizeToUtc(asOfDate);

        // Step 1: Fetch snapshots for this source up to as-of (materialise so we can safely
        // use an in-memory lookup without tripping EF translation).
        var snapshots = await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.ItsmSource == itsmSource && s.SnapshotDate <= asOfUtc)
            .Select(s => new { s.Id, s.SnapshotDate })
            .ToListAsync();

        if (snapshots.Count == 0)
            return [];

        var snapshotIdSet = snapshots.Select(s => s.Id).ToHashSet();
        var snapshotDateLookup = snapshots.ToDictionary(s => s.Id, s => s.SnapshotDate);

        // Step 2: Fetch all candidate snapshot-ticket rows for the company, then select
        // the latest snapshot per ticket in-memory (EF-safe and provider-agnostic).
        var snapshotTickets = await _context.SnapshotTickets
            .AsNoTracking()
            .Where(st => st.CompanyName == companyName && snapshotIdSet.Contains(st.SnapshotId))
            .Select(st => new { st.TicketKey, st.SnapshotId })
            .ToListAsync();

        if (snapshotTickets.Count == 0)
            return [];

        var latestTickets = snapshotTickets
            .GroupBy(st => st.TicketKey, StringComparer.Ordinal)
            .Select(g => g
                .Select(x => new
                {
                    TicketKey = x.TicketKey,
                    SnapshotId = x.SnapshotId,
                    SnapshotDate = snapshotDateLookup.TryGetValue(x.SnapshotId, out var sd) ? sd : DateTime.MinValue
                })
                .OrderByDescending(x => x.SnapshotDate)
                .ThenByDescending(x => x.SnapshotId)
                .First())
            .ToList();

        var ticketKeySet = latestTickets.Select(t => t.TicketKey).ToHashSet(StringComparer.Ordinal);

        // Step 3: Bulk fetch all field-change rows, then reconstruct the latest field values
        // per (ticket, field) in-memory.
        var fieldChanges = await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.CompanyName == companyName
                      && ticketKeySet.Contains(fc.TicketKey)
                      && fc.ObservedAt <= asOfUtc)
            .Select(fc => new { fc.TicketKey, fc.FieldName, fc.FieldValue, fc.ObservedAt })
            .ToListAsync();

        var latestFields = fieldChanges
            .GroupBy(fc => (fc.TicketKey, fc.FieldName))
            .Select(g => g.OrderByDescending(x => x.ObservedAt).First())
            .ToList();

        var fieldsByTicket = latestFields
            .GroupBy(f => f.TicketKey, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IDictionary<string, string?>)g.ToDictionary(f => f.FieldName, f => f.FieldValue, StringComparer.Ordinal),
                StringComparer.Ordinal);

        var maxObservedByTicket = latestFields
            .GroupBy(f => f.TicketKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Max(f => f.ObservedAt), StringComparer.Ordinal);

        return latestTickets
            .Select(row =>
            {
                var fields = fieldsByTicket.TryGetValue(row.TicketKey, out var f)
                    ? f
                    : new Dictionary<string, string?>();

                AddDerivedSystemFields(row.TicketKey, fields);

                return new Ticket
                {
                    TicketKey = row.TicketKey,
                    CompanyName = companyName,
                    ItsmSource = itsmSource,
                    Fields = fields,
                    ObservedAt = maxObservedByTicket.TryGetValue(row.TicketKey, out var obs) ? obs : DateTime.MinValue,
                    SnapshotId = row.SnapshotId,
                    SnapshotDate = row.SnapshotDate
                };
            })
            .OrderBy(t => t.TicketKey)
            .ToList();
    }

    /// <summary>
    /// Retrieves the latest known version of every ticket for an ITSM source as-of a point in time.
    /// This is used by the Tickets page ("As of" search).
    /// </summary>
    public async Task<IEnumerable<Ticket>> GetTicketsFilteredAsync(
        string itsmSource,
        DateTime asOfDate,
        IEnumerable<TicketFieldFilter>? fieldFilters = null)
    {
        if (string.IsNullOrWhiteSpace(itsmSource))
            throw new ArgumentException("ITSM source is required.", nameof(itsmSource));
        if (asOfDate == default)
            throw new ArgumentException("asOfDate is required.", nameof(asOfDate));
    
        DateTime asOfUtc = NormalizeToUtc(asOfDate);

        // Step 1: Load snapshots for this source up to as-of.
        var snapshots = await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.ItsmSource == itsmSource && s.SnapshotDate <= asOfUtc)
            .Select(s => new { s.Id, s.SnapshotDate })
            .ToListAsync();

        if (snapshots.Count == 0)
            return [];

        var snapshotIdSet = snapshots.Select(s => s.Id).ToHashSet();
        var snapshotDateLookup = snapshots.ToDictionary(s => s.Id, s => s.SnapshotDate);

        // Step 2: Load candidate snapshot-ticket rows then pick latest per (company, ticket).
        // We group by company+ticketKey to avoid cross-tenant collisions.
        var snapshotTickets = await _context.SnapshotTickets
            .AsNoTracking()
            .Where(st => snapshotIdSet.Contains(st.SnapshotId))
            .Select(st => new { st.CompanyName, st.TicketKey, st.SnapshotId })
            .ToListAsync();

        if (snapshotTickets.Count == 0)
            return [];

        var latestTickets = snapshotTickets
            .GroupBy(st => (st.CompanyName, st.TicketKey))
            .Select(g => g
                .Select(x => new
                {
                    CompanyName = x.CompanyName,
                    TicketKey = x.TicketKey,
                    SnapshotId = x.SnapshotId,
                    SnapshotDate = snapshotDateLookup.TryGetValue(x.SnapshotId, out var sd) ? sd : DateTime.MinValue
                })
                .OrderByDescending(x => x.SnapshotDate)
                .ThenByDescending(x => x.SnapshotId)
                .First())
            .ToList();

        var ticketKeySet = latestTickets.Select(t => t.TicketKey).ToHashSet(StringComparer.Ordinal);
        var companyNameSet = latestTickets.Select(t => t.CompanyName).ToHashSet(StringComparer.Ordinal);

        // Step 3: Load field changes for these tickets up to as-of then reconstruct latest values.
        var fieldChanges = await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => ticketKeySet.Contains(fc.TicketKey)
                      && companyNameSet.Contains(fc.CompanyName)
                      && fc.ObservedAt <= asOfUtc)
            .Select(fc => new { fc.CompanyName, fc.TicketKey, fc.FieldName, fc.FieldValue, fc.ObservedAt })
            .ToListAsync();

        var latestFields = fieldChanges
            .GroupBy(fc => (fc.CompanyName, fc.TicketKey, fc.FieldName))
            .Select(g => g.OrderByDescending(x => x.ObservedAt).First())
            .ToList();

        var fieldsByCompanyTicket = latestFields
            .GroupBy(f => (f.CompanyName, f.TicketKey))
            .ToDictionary(
                g => g.Key,
                g => (IDictionary<string, string?>)g.ToDictionary(f => f.FieldName, f => f.FieldValue, StringComparer.Ordinal));

        var maxObservedByCompanyTicket = latestFields
            .GroupBy(f => (f.CompanyName, f.TicketKey))
            .ToDictionary(g => g.Key, g => g.Max(f => f.ObservedAt));

        var results = latestTickets
            .Select(row =>
            {
                var key = (row.CompanyName, row.TicketKey);

                var fields = fieldsByCompanyTicket.TryGetValue(key, out var f)
                    ? f
                    : new Dictionary<string, string?>();

                AddDerivedSystemFields(row.TicketKey, fields);

                return new Ticket
                {
                    TicketKey = row.TicketKey,
                    CompanyName = row.CompanyName,
                    ItsmSource = itsmSource,
                    Fields = fields,
                    ObservedAt = maxObservedByCompanyTicket.TryGetValue(key, out var obs) ? obs : DateTime.MinValue,
                    SnapshotId = row.SnapshotId,
                    SnapshotDate = row.SnapshotDate,
                };
            })
            .ToList();

        // Apply field filters if provided (case-insensitive substring match).
        if (fieldFilters is not null)
        {
            var filters = fieldFilters
                .Where(f => !string.IsNullOrWhiteSpace(f.FieldName) && !string.IsNullOrWhiteSpace(f.Value))
                .ToList();

            if (filters.Count > 0)
            {
                results = results
                    .Where(ticket => filters.All(ff => TicketMatchesFilter(ticket, ff)))
                    .ToList();
            }
        }

        return results;
    }
    
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

        var ticketKeySet = ticketRows.Select(t => t.TicketKey).ToHashSet(StringComparer.Ordinal);
        var companySet = ticketRows.Select(t => t.CompanyName).ToHashSet(StringComparer.Ordinal);

        // Reconstruct ticket fields as-of the snapshot timestamp.
        var fieldRows = await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.ObservedAt <= snapshot.SnapshotDate
                      && ticketKeySet.Contains(fc.TicketKey)
                      && companySet.Contains(fc.CompanyName))
            .Select(fc => new { fc.CompanyName, fc.TicketKey, fc.FieldName, fc.FieldValue, fc.ObservedAt })
            .ToListAsync();

        var latestFields = fieldRows
            .GroupBy(f => (f.CompanyName, f.TicketKey, f.FieldName))
            .Select(g => g.OrderByDescending(x => x.ObservedAt).First())
            .ToList();

        // Group fields by (company, ticketKey)
        var fieldsByCompanyTicket = latestFields
            .GroupBy(f => (f.CompanyName, f.TicketKey))
            .ToDictionary(
                g => g.Key,
                g => (IDictionary<string, string?>)g.ToDictionary(f => f.FieldName, f => f.FieldValue, StringComparer.Ordinal));

        var maxObservedByCompanyTicket = latestFields
            .GroupBy(f => (f.CompanyName, f.TicketKey))
            .ToDictionary(g => g.Key, g => g.Max(f => f.ObservedAt));

        return ticketRows
            .Select(row =>
            {
                var key = (row.CompanyName, row.TicketKey);

                var fields = fieldsByCompanyTicket.TryGetValue(key, out var f)
                    ? f
                    : new Dictionary<string, string?>();

                AddDerivedSystemFields(row.TicketKey, fields);

                return new Ticket
                {
                    TicketKey = row.TicketKey,
                    CompanyName = row.CompanyName,
                    ItsmSource = snapshot.ItsmSource,
                    Fields = fields,
                    ObservedAt = maxObservedByCompanyTicket.TryGetValue(key, out var obs) ? obs : snapshot.SnapshotDate,
                    SnapshotId = snapshotId,
                    SnapshotDate = snapshot.SnapshotDate
                };
            })
            .OrderBy(t => t.CompanyName)
            .ThenBy(t => t.TicketKey)
            .ToList();
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

    /// <summary>Retrieves the complete change history for a ticket (all fields).</summary>
    public async Task<IEnumerable<FieldChange>> GetTicketHistoryAsync(string companyName, string ticketKey)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));
        if (string.IsNullOrWhiteSpace(ticketKey))
            throw new ArgumentException("Ticket key is required.", nameof(ticketKey));

        return await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.CompanyName == companyName && fc.TicketKey == ticketKey)
            .OrderByDescending(fc => fc.ObservedAt)
            .ThenBy(fc => fc.FieldName)
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



    private static void AddDerivedSystemFields(string ticketKey, IDictionary<string, string?> fields)
    {
        if (TicketKeyFactory.TryParse(ticketKey, out _, out _, out var ticketNumber))
        {
            fields["ticket_number"] = ticketNumber;
        }
    }
    private static bool TicketMatchesFilter(Ticket ticket, TicketFieldFilter filter)
    {
        var field = (filter.FieldName ?? string.Empty).Trim();
        var value = (filter.Value ?? string.Empty).Trim();

        if (field.Length == 0 || value.Length == 0)
            return true;

        // System fields / special cases
        if (string.Equals(field, "company", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "company_name", StringComparison.OrdinalIgnoreCase))
        {
            return ticket.CompanyName.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(field, "itsm_source", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "source", StringComparison.OrdinalIgnoreCase))
        {
            return ticket.ItsmSource.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(field, "ticket_key", StringComparison.OrdinalIgnoreCase))
        {
            // Legacy: some mappings used 'ticket_key' as the canonical name for the ticket number.
            if (ticket.TicketKey.Contains(value, StringComparison.OrdinalIgnoreCase))
                return true;

            if (TryGetFieldValue(ticket.Fields, "ticket_number", out var ticketNumber) && !string.IsNullOrEmpty(ticketNumber))
                return ticketNumber.Contains(value, StringComparison.OrdinalIgnoreCase);

            return false;
        }

        if (string.Equals(field, "ticket_number", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "number", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetFieldValue(ticket.Fields, "ticket_number", out var ticketNumber) && !string.IsNullOrEmpty(ticketNumber))
                return ticketNumber.Contains(value, StringComparison.OrdinalIgnoreCase);

            // Fallback: match the composite ticket key.
            return ticket.TicketKey.Contains(value, StringComparison.OrdinalIgnoreCase);
        }

        // Normal mapped fields
        if (TryGetFieldValue(ticket.Fields, field, out var v) && v is not null)
            return v.Contains(value, StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private static bool TryGetFieldValue(IDictionary<string, string?> fields, string fieldName, out string? value)
    {
        if (fields.TryGetValue(fieldName, out value))
            return true;

        // Cheap case-insensitive fallback (dictionary is Ordinal in most of our paths)
        foreach (var kv in fields)
        {
            if (string.Equals(kv.Key, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                value = kv.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

}
