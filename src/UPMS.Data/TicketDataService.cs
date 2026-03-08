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

        var snapshots = await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.ItsmSource == itsmSource && s.SnapshotDate <= asOfUtc)
            .Select(s => new { s.Id, s.SnapshotDate })
            .ToListAsync();

        if (snapshots.Count == 0)
            return [];

        var snapshotIdSet = snapshots.Select(s => s.Id).ToHashSet();
        var snapshotDateLookup = snapshots.ToDictionary(s => s.Id, s => s.SnapshotDate);

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

        var fieldChanges = await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.CompanyName == companyName
                      && ticketKeySet.Contains(fc.TicketKey)
                      && fc.ObservedAt <= asOfUtc)
            .Select(fc => new { fc.Id, fc.TicketKey, fc.FieldName, fc.CanonicalFieldName, fc.FieldValue, fc.ObservedAt })
            .ToListAsync();

        var latestFields = fieldChanges
            .GroupBy(fc => (fc.TicketKey, FieldName: GetDisplayFieldName(fc.CanonicalFieldName, fc.FieldName)))
            .Select(g => g
                .OrderByDescending(x => x.ObservedAt)
                .ThenByDescending(x => x.Id)
                .First())
            .ToList();

        var fieldsByTicket = latestFields
            .GroupBy(f => f.TicketKey, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IDictionary<string, string?>)g.ToDictionary(
                    f => GetDisplayFieldName(f.CanonicalFieldName, f.FieldName),
                    f => f.FieldValue,
                    StringComparer.Ordinal),
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

        var snapshots = await _context.Snapshots
            .AsNoTracking()
            .Where(s => s.ItsmSource == itsmSource && s.SnapshotDate <= asOfUtc)
            .Select(s => new { s.Id, s.SnapshotDate })
            .ToListAsync();

        if (snapshots.Count == 0)
            return [];

        var snapshotIdSet = snapshots.Select(s => s.Id).ToHashSet();
        var snapshotDateLookup = snapshots.ToDictionary(s => s.Id, s => s.SnapshotDate);

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

        var fieldChanges = await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => ticketKeySet.Contains(fc.TicketKey)
                      && companyNameSet.Contains(fc.CompanyName)
                      && fc.ObservedAt <= asOfUtc)
            .Select(fc => new { fc.Id, fc.CompanyName, fc.TicketKey, fc.FieldName, fc.CanonicalFieldName, fc.FieldValue, fc.ObservedAt })
            .ToListAsync();

        var latestFields = fieldChanges
            .GroupBy(fc => (fc.CompanyName, fc.TicketKey, FieldName: GetDisplayFieldName(fc.CanonicalFieldName, fc.FieldName)))
            .Select(g => g
                .OrderByDescending(x => x.ObservedAt)
                .ThenByDescending(x => x.Id)
                .First())
            .ToList();

        var fieldsByCompanyTicket = latestFields
            .GroupBy(f => (f.CompanyName, f.TicketKey))
            .ToDictionary(
                g => g.Key,
                g => (IDictionary<string, string?>)g.ToDictionary(
                    f => GetDisplayFieldName(f.CanonicalFieldName, f.FieldName),
                    f => f.FieldValue,
                    StringComparer.Ordinal));

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

        var fieldRows = await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.ObservedAt <= snapshot.SnapshotDate
                      && ticketKeySet.Contains(fc.TicketKey)
                      && companySet.Contains(fc.CompanyName))
            .Select(fc => new { fc.Id, fc.CompanyName, fc.TicketKey, fc.FieldName, fc.CanonicalFieldName, fc.FieldValue, fc.ObservedAt })
            .ToListAsync();

        var latestFields = fieldRows
            .GroupBy(f => (f.CompanyName, f.TicketKey, FieldName: GetDisplayFieldName(f.CanonicalFieldName, f.FieldName)))
            .Select(g => g
                .OrderByDescending(x => x.ObservedAt)
                .ThenByDescending(x => x.Id)
                .First())
            .ToList();

        var fieldsByCompanyTicket = latestFields
            .GroupBy(f => (f.CompanyName, f.TicketKey))
            .ToDictionary(
                g => g.Key,
                g => (IDictionary<string, string?>)g.ToDictionary(
                    f => GetDisplayFieldName(f.CanonicalFieldName, f.FieldName),
                    f => f.FieldValue,
                    StringComparer.Ordinal));

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

    public async Task<Snapshot?> GetSnapshotByIdAsync(Guid snapshotId)
    {
        if (snapshotId == Guid.Empty)
            return null;

        return await _context.Snapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == snapshotId);
    }

    /// <summary>Retrieves the complete change history for a specific field of a ticket.</summary>
    public async Task<IEnumerable<FieldChange>> GetTicketFieldHistoryAsync(
        string companyName,
        string ticketKey,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));
        if (string.IsNullOrWhiteSpace(ticketKey))
            throw new ArgumentException("Ticket key is required.", nameof(ticketKey));
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name is required.", nameof(fieldName));

        var definitionLookup = await GetCanonicalFieldDefinitionLookupAsync();

        var rows = await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.CompanyName == companyName && fc.TicketKey == ticketKey)
            .OrderBy(fc => fc.ObservedAt)
            .ThenBy(fc => fc.Id)
            .ToListAsync();

        return rows
            .Where(fc => FieldNameMatches(fc, fieldName))
            .Select(fc => WithCanonicalMetadata(fc, definitionLookup))
            .ToList();
    }

    /// <summary>Retrieves the complete change history for a ticket (all fields).</summary>
    public async Task<IEnumerable<FieldChange>> GetTicketHistoryAsync(string companyName, string ticketKey)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));
        if (string.IsNullOrWhiteSpace(ticketKey))
            throw new ArgumentException("Ticket key is required.", nameof(ticketKey));

        var definitionLookup = await GetCanonicalFieldDefinitionLookupAsync();

        var rows = await _context.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.CompanyName == companyName && fc.TicketKey == ticketKey)
            .ToListAsync();

        return rows
            .Select(fc => WithCanonicalMetadata(fc, definitionLookup))
            .OrderByDescending(fc => fc.ObservedAt)
            .ThenBy(fc => fc.DisplayFieldName, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private async Task<IReadOnlyDictionary<string, CanonicalFieldDefinition>> GetCanonicalFieldDefinitionLookupAsync()
    {
        var rows = await _context.CanonicalFieldDefinitions
            .AsNoTracking()
            .ToListAsync();

        return rows.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetDisplayFieldName(string? canonicalFieldName, string sourceFieldName)
    {
        if (!string.IsNullOrWhiteSpace(canonicalFieldName))
            return canonicalFieldName.Trim();

        return sourceFieldName?.Trim() ?? string.Empty;
    }

    private static bool FieldNameMatches(FieldChange change, string fieldName)
    {
        return string.Equals(change.FieldName, fieldName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(change.DisplayFieldName, fieldName, StringComparison.OrdinalIgnoreCase);
    }

    private static FieldChange WithCanonicalMetadata(
        FieldChange change,
        IReadOnlyDictionary<string, CanonicalFieldDefinition> definitionLookup)
    {
        var copy = new FieldChange
        {
            Id = change.Id,
            CompanyName = change.CompanyName,
            TicketKey = change.TicketKey,
            FieldName = change.FieldName,
            CanonicalFieldName = change.CanonicalFieldName,
            FieldValue = change.FieldValue,
            ObservedAt = change.ObservedAt,
            SnapshotId = change.SnapshotId
        };

        if (definitionLookup.TryGetValue(copy.DisplayFieldName, out var definition))
        {
            copy.RegisteredDataType = definition.DataType;
            copy.IsValueValid = CanonicalFieldValueValidator.IsValid(definition.DataType, copy.FieldValue);
        }

        return copy;
    }

    private static void AddDerivedSystemFields(string ticketKey, IDictionary<string, string?> fields)
    {
        if (TicketKeyFactory.TryParse(ticketKey, out _, out _, out var ticketNumber))
        {
            fields["ticket_number"] = ticketNumber;
            fields["Number"] = ticketNumber;
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
            return ValueMatches(ticket.CompanyName, value, filter.DataType);
        }

        if (string.Equals(field, "itsm_source", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "source", StringComparison.OrdinalIgnoreCase))
        {
            return ValueMatches(ticket.ItsmSource, value, filter.DataType);
        }

        if (string.Equals(field, "ticket_key", StringComparison.OrdinalIgnoreCase))
        {
            // Legacy: some mappings used 'ticket_key' as the canonical name for the ticket number.
            if (ValueMatches(ticket.TicketKey, value, CanonicalFieldDataType.Text))
                return true;

            if (TryGetFieldValue(ticket.Fields, "ticket_number", out var ticketNumber) && !string.IsNullOrEmpty(ticketNumber))
                return ValueMatches(ticketNumber, value, filter.DataType ?? CanonicalFieldDataType.Text);

            return false;
        }

        if (string.Equals(field, "ticket_number", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field, "number", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetFieldValue(ticket.Fields, "ticket_number", out var ticketNumber) && !string.IsNullOrEmpty(ticketNumber))
                return ValueMatches(ticketNumber, value, filter.DataType ?? CanonicalFieldDataType.Text);

            // Fallback: match the composite ticket key.
            return ValueMatches(ticket.TicketKey, value, CanonicalFieldDataType.Text);
        }

        // Normal mapped fields
        if (TryGetFieldValue(ticket.Fields, field, out var v) && v is not null)
            return ValueMatches(v, value, filter.DataType);

        return false;
    }

    private static bool ValueMatches(string? actual, string expected, CanonicalFieldDataType? dataType)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
            return false;

        actual = actual.Trim();
        expected = expected.Trim();

        switch (dataType)
        {
            case CanonicalFieldDataType.Boolean:
                if (TryParseBoolean(actual, out var actualBool) && TryParseBoolean(expected, out var expectedBool))
                    return actualBool == expectedBool;
                break;
            case CanonicalFieldDataType.Integer:
                if (long.TryParse(actual, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actualInt)
                    && long.TryParse(expected, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expectedInt))
                {
                    return actualInt == expectedInt;
                }
                break;
            case CanonicalFieldDataType.Decimal:
                if (decimal.TryParse(actual, NumberStyles.Number, CultureInfo.InvariantCulture, out var actualDecimal)
                    && decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var expectedDecimal))
                {
                    return actualDecimal == expectedDecimal;
                }
                break;
            case CanonicalFieldDataType.DateTime:
                if (TryParseFlexibleDateTime(actual, out var actualDateTime)
                    && TryParseFlexibleDateTime(expected, out var expectedDateTime))
                {
                    if (expected.Length <= 10)
                        return actualDateTime.Date == expectedDateTime.Date;

                    return actualDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
                        == expectedDateTime.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);
                }
                break;
        }

        return actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseBoolean(string input, out bool value)
    {
        if (bool.TryParse(input, out value))
            return true;

        switch (input.Trim().ToLowerInvariant())
        {
            case "1":
            case "yes":
            case "y":
            case "on":
                value = true;
                return true;
            case "0":
            case "no":
            case "n":
            case "off":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    private static bool TryParseFlexibleDateTime(string input, out DateTime value)
    {
        if (DateTime.TryParseExact(
            input,
            [
                "yyyy-MM-ddTHH:mm",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd"
            ],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            value = NormalizeToUtc(parsed);
            return true;
        }

        if (DateTime.TryParse(
            input,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsed))
        {
            value = NormalizeToUtc(parsed);
            return true;
        }

        value = default;
        return false;
    }

    private static readonly IReadOnlyDictionary<string, string[]> FieldAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Number"] = ["Number", "number", "ticket_number", "ticket_key"],
            ["State"] = ["State", "state", "status", "incident_state"],
            ["Priority"] = ["Priority", "priority"],
            ["Assigned To"] = ["Assigned To", "assigned_to", "assignee"],
            ["Assignment Group"] = ["Assignment Group", "assignment_group"],
            ["Short Description"] = ["Short Description", "short_description", "title"],
            ["Description"] = ["Description", "description"],
            ["Category"] = ["Category", "category"],
            ["Subcategory"] = ["Subcategory", "subcategory", "sub_category"],
            ["Business Service"] = ["Business Service", "business_service"],
            ["Service Offering"] = ["Service Offering", "service_offering"],
            ["Opened At"] = ["Opened At", "opened_at", "opened_date", "created_at", "created_date", "Created On"],
            ["Created On"] = ["Created On", "created_on", "created_at", "created_date"],
            ["Updated On"] = ["Updated On", "updated_on", "updated_at", "updated_date", "last_updated"],
            ["Resolved At"] = ["Resolved At", "resolved_at", "resolved_date"],
            ["Closed At"] = ["Closed At", "closed_at", "closed_date"],
            ["Company"] = ["Company", "company", "company_name"]
        };

    private static bool TryGetFieldValue(IDictionary<string, string?> fields, string fieldName, out string? value)
    {
        foreach (var alias in GetFieldAliases(fieldName))
        {
            if (fields.TryGetValue(alias, out value))
                return true;

            // Cheap case-insensitive fallback (dictionary is Ordinal in most of our paths)
            foreach (var kv in fields)
            {
                if (string.Equals(kv.Key, alias, StringComparison.OrdinalIgnoreCase))
                {
                    value = kv.Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    private static IReadOnlyList<string> GetFieldAliases(string fieldName)
    {
        if (FieldAliases.TryGetValue(fieldName, out var aliases))
            return aliases;

        foreach (var pair in FieldAliases)
        {
            if (pair.Value.Any(alias => string.Equals(alias, fieldName, StringComparison.OrdinalIgnoreCase)))
                return pair.Value;
        }

        return [fieldName];
    }

}
