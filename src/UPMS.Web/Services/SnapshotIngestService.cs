namespace UPMS.Web.Services;

using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UPMS.Data;
using UPMS.Web;

/// <summary>
/// Parses uploaded flat-table CSV and JSON snapshot files and persists extracted ticket and
/// field-change data. Field names are mapped to canonical names via <see cref="IItsmSourceService"/>.
/// Company is read from the CSV/JSON row data (the column mapped to canonical "company") — it is
/// not passed as a parameter.
/// </summary>
public class SnapshotIngestService : ISnapshotIngestService
{
    private readonly ICommandRepository _commandRepository;
    private readonly IItsmSourceService _sourceService;
    private readonly UpmsDbContext _dbContext;
    private readonly ILogger<SnapshotIngestService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public SnapshotIngestService(
        ICommandRepository commandRepository,
        IItsmSourceService sourceService,
        UpmsDbContext dbContext,
        ILogger<SnapshotIngestService> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _commandRepository = commandRepository ?? throw new ArgumentNullException(nameof(commandRepository));
        _sourceService     = sourceService     ?? throw new ArgumentNullException(nameof(sourceService));
        _dbContext         = dbContext         ?? throw new ArgumentNullException(nameof(dbContext));
        _logger            = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<IngestResult> IngestCsvAsync(
        Stream csvStream,
        string itsmSourceName,
        DateOnly snapshotDate,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSourceName);

        try
        {
            using var reader  = new StreamReader(csvStream, Encoding.UTF8, leaveOpen: true);
            var warnings      = new List<string>();

            // ── 1. Read header row ──────────────────────────────────────────
            string? headerLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return IngestResult.Failure("CSV file is empty or has no header row.");
            }

            string[] headers = headerLine.Split(',').Select(h => h.Trim()).ToArray();

            // ── 2. Validate required fields ─────────────────────────────────
            IReadOnlyList<string> requiredFields = await _sourceService.GetRequiredFieldsAsync(itsmSourceName);
            var missingRequired = requiredFields
                .Where(req => !headers.Contains(req, StringComparer.Ordinal))
                .ToList();

            if (missingRequired.Count > 0)
            {
                return IngestResult.Failure(
                    $"Missing required fields: {string.Join(", ", missingRequired)}");
            }

            // ── 3. Resolve canonical names for each header ──────────────────
            // canonical[i] = canonical name for headers[i], or null if no mapping
            string?[] canonicalHeaders = new string?[headers.Length];
            for (int i = 0; i < headers.Length; i++)
            {
                canonicalHeaders[i] = await _sourceService.GetCanonicalNameAsync(itsmSourceName, headers[i]);
            }

            // Locate the column index for the canonical "Number" and "Company" fields.
            // Legacy aliases (ticket_number / ticket_key / company) are still recognised.
            int ticketNumberColIndex = FindCanonicalIndex(canonicalHeaders, CanonicalAliasesForTicketNumber);
            int companyColIndex   = FindCanonicalIndex(canonicalHeaders, CanonicalAliasesForCompany);

            if (ticketNumberColIndex < 0)
            {
                return IngestResult.Failure(
                    "No column is mapped to canonical name 'Number' (legacy aliases 'ticket_number' / 'ticket_key' are also accepted) for this ITSM source. " +
                    "Add a field mapping for Number before uploading.");
            }

            if (companyColIndex < 0)
            {
                return IngestResult.Failure(
                    "No column is mapped to canonical name 'Company' for this ITSM source. " +
                    "Add a field mapping for Company before uploading.");
            }

            // ── 4. Parse data rows ──────────────────────────────────────────
            // Each row is a ticket; each column is a field.
            // rows: list of (ticketKey, companyName, columns[])
            var parsedRows = new List<(string TicketKey, string CompanyName, string[] Columns)>();
            int lineNumber = 1;

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                lineNumber++;

                if (string.IsNullOrWhiteSpace(line))
                {
                    warnings.Add($"Line {lineNumber}: blank row skipped.");
                    continue;
                }

                string[] cols = line.Split(',').Select(c => c.Trim()).ToArray();

                if (cols.Length <= Math.Max(ticketNumberColIndex, companyColIndex))
                {
                    warnings.Add($"Line {lineNumber}: insufficient columns, row skipped.");
                    continue;
                }

                string ticketNumber = cols[ticketNumberColIndex];
                string companyName = cols[companyColIndex];

                if (string.IsNullOrWhiteSpace(ticketNumber))
                {
                    warnings.Add($"Line {lineNumber}: empty ticket number, row skipped.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(companyName))
                {
                    warnings.Add($"Line {lineNumber}: empty company value, row skipped.");
                    continue;
                }

                string ticketKey;
                try
                {
                    ticketKey = TicketKeyFactory.Compose(itsmSourceName, companyName, ticketNumber);
                }
                catch (ArgumentException ex)
                {
                    warnings.Add($"Line {lineNumber}: {ex.Message}, row skipped.");
                    continue;
                }

                parsedRows.Add((ticketKey, companyName, cols));
            }

            // ── 5. Persist ──────────────────────────────────────────────────
            return await PersistFlatTableRowsAsync(
                parsedRows, headers, canonicalHeaders,
                itsmSourceName, snapshotDate,
                warnings, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest CSV for source {Source}", itsmSourceName);
            return IngestResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<IngestResult> IngestJsonAsync(
        Stream jsonStream,
        string itsmSourceName,
        DateOnly snapshotDate,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSourceName);

        try
        {
            var warnings  = new List<string>();
            var parsedRows = new List<(string TicketKey, string CompanyName, Dictionary<string, string?> Fields)>();

            using JsonDocument document = await JsonDocument.ParseAsync(jsonStream, cancellationToken: ct);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return IngestResult.Failure("JSON root element must be an array.");
            }

            // Validate required fields are available
            IReadOnlyList<string> requiredFields = await _sourceService.GetRequiredFieldsAsync(itsmSourceName);

            // Pre-fetch all mappings for this source to avoid per-field DB lookups
            var mappings = await _sourceService.GetSourceDefinitionAsync(itsmSourceName);
            var canonicalLookup = mappings.Mappings.ToDictionary(
                m => m.SourceFieldName,
                m => m.CanonicalFieldName,
                StringComparer.OrdinalIgnoreCase);

            int elementIndex = 0;
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                elementIndex++;

                if (element.ValueKind != JsonValueKind.Object)
                {
                    warnings.Add($"Element {elementIndex}: not a JSON object, skipped.");
                    continue;
                }

                // Collect all fields from the JSON object
                var fields = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    fields[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : prop.Value.ToString();
                }

                // Validate required fields
                var missingRequired = requiredFields
                    .Where(req => !fields.ContainsKey(req))
                    .ToList();

                if (missingRequired.Count > 0)
                {
                    warnings.Add($"Element {elementIndex}: missing required fields: {string.Join(", ", missingRequired)}, skipped.");
                    continue;
                }

                // Resolve ticket number and company using pre-fetched canonical lookup
                // Legacy aliases are still recognised.
                string? ticketNumber = null;
                string? companyName = null;
                foreach (var (sourceField, value) in fields)
                {
                    if (ticketNumber is null && canonicalLookup.TryGetValue(sourceField, out var can1) && IsTicketNumberCanonical(can1))
                        ticketNumber = value;

                    if (companyName is null && canonicalLookup.TryGetValue(sourceField, out var can2) && IsCompanyCanonical(can2))
                        companyName = value;

                    if (ticketNumber != null && companyName != null)
                        break;
                }

                ticketNumber = NormalizeValue(ticketNumber);
                companyName = NormalizeValue(companyName);

                if (string.IsNullOrWhiteSpace(ticketNumber))
                {
                    warnings.Add($"Element {elementIndex}: could not resolve Number value, skipped.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(companyName))
                {
                    warnings.Add($"Element {elementIndex}: could not resolve company value, skipped.");
                    continue;
                }

                string ticketKey;
                try
                {
                    ticketKey = TicketKeyFactory.Compose(itsmSourceName, companyName, ticketNumber);
                }
                catch (ArgumentException ex)
                {
                    warnings.Add($"Element {elementIndex}: {ex.Message}, skipped.");
                    continue;
                }

                parsedRows.Add((ticketKey, companyName, fields));
            }

            return await PersistJsonRowsAsync(
                parsedRows, itsmSourceName, canonicalLookup, snapshotDate, warnings, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON for source {Source}", itsmSourceName);
            return IngestResult.Failure($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ingest JSON for source {Source}", itsmSourceName);
            return IngestResult.Failure(ex.Message);
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private async Task<IngestResult> PersistFlatTableRowsAsync(
        List<(string TicketKey, string CompanyName, string[] Columns)> rows,
        string[] headers,
        string?[] canonicalHeaders,
        string itsmSourceName,
        DateOnly snapshotDate,
        List<string> warnings,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return new IngestResult
            {
                Success = true,
                SnapshotId = Guid.Empty,
                TicketsIngested = 0,
                FieldChangesRecorded = 0,
                Warnings = warnings.AsReadOnly()
            };
        }

        DateTime snapshotDateTime = snapshotDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Create snapshot record via ICommandRepository
        var snapshot = await _commandRepository.CreateSnapshotAsync(
            new Snapshot
            {
                Id           = Guid.NewGuid(),
                ItsmSource   = itsmSourceName,
                SnapshotDate = snapshotDateTime,
                UploadedBy   = "web-upload",
                UploadedAt   = DateTime.UtcNow,
            },
            ct);

        Guid snapshotId = snapshot.Id;

        // Figure out which columns represent company and ticket-number metadata.
        // Legacy aliases are still recognised.
        int ticketNumberColIndex = FindCanonicalIndex(canonicalHeaders, CanonicalAliasesForTicketNumber);
        int companyColIndex = FindCanonicalIndex(canonicalHeaders, CanonicalAliasesForCompany);

        // Collect distinct (ticketKey, companyName) pairs and build SnapshotTicket entities
        var ticketEntries = rows
            .Select(r => (TicketKey: r.TicketKey, CompanyName: r.CompanyName))
            .Distinct()
            .ToList();

        var snapshotTickets = ticketEntries
            .Select(e => new SnapshotTicket
            {
                Id          = Guid.NewGuid(),
                SnapshotId  = snapshotId,
                CompanyName = e.CompanyName,
                TicketKey   = e.TicketKey,
            })
            .ToList();

        await _commandRepository.AddSnapshotTicketsAsync(snapshotTickets, ct);

        // Preload last-known values so we only record *actual* field changes (not a full re-dump each snapshot).
        var candidateFieldNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            if (i == companyColIndex)
                continue;

            string? canonical = canonicalHeaders[i];

            // Don't store Number / ticket identity aliases as a field change; it's part of the ticket identity.
            if (i == ticketNumberColIndex
                || IsTicketNumberCanonical(canonical))
            {
                continue;
            }

            string fieldName = canonical ?? headers[i];
            candidateFieldNames.Add(fieldName);
        }

        var lastKnownValues = await LoadLatestFieldValuesAsync(ticketEntries, candidateFieldNames, snapshotDateTime, ct);

        // Batch-build and persist only the deltas
        var fieldChanges = new List<FieldChange>();
        foreach (var (ticketKey, companyName, cols) in rows)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                // Don't store company as a field (it's already stored as metadata)
                if (i == companyColIndex)
                    continue;

                string? canonical = canonicalHeaders[i];

                // Don't store Number / ticket identity aliases as a field change; it's part of the ticket identity.
                if (i == ticketNumberColIndex
                    || IsTicketNumberCanonical(canonical))
                {
                    continue;
                }

                string fieldName = canonical ?? headers[i];

                string? fieldValue = i < cols.Length ? NormalizeValue(cols[i]) : null;

                var key = (CompanyName: companyName, TicketKey: ticketKey, FieldName: fieldName);

                if (lastKnownValues.TryGetValue(key, out var previousValue))
                {
                    if (string.Equals(previousValue, fieldValue, StringComparison.Ordinal))
                        continue;
                }
                else
                {
                    // Don't store "first value is null" noise.
                    if (fieldValue is null)
                        continue;
                }

                fieldChanges.Add(new FieldChange
                {
                    CompanyName = companyName,
                    TicketKey   = ticketKey,
                    FieldName   = fieldName,
                    FieldValue  = fieldValue,
                    ObservedAt  = snapshotDateTime,
                    SnapshotId  = snapshotId,
                });

                // Keep the dictionary updated so repeated values inside the same ingest don't create duplicates.
                lastKnownValues[key] = fieldValue;
            }
        }

        await _commandRepository.RecordFieldChangesAsync(fieldChanges, ct);

        // Log acting user and correlation will be included from logging scope middleware
        string? actor = _httpContextAccessor?.HttpContext?.User?.FindFirst("preferred_username")?.Value
                        ?? _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Upn)?.Value
                        ?? _httpContextAccessor?.HttpContext?.User?.FindFirst("name")?.Value
                        ?? _httpContextAccessor?.HttpContext?.User?.FindFirst("oid")?.Value
                        ?? "anonymous";

        _logger.LogInformation("Snapshot {SnapshotId} ingested by {Actor}: {Tickets} tickets, {Changes} field changes",
            snapshotId, actor, ticketEntries.Count, fieldChanges.Count);

        return new IngestResult
        {
            Success = true,
            SnapshotId = snapshotId,
            TicketsIngested = ticketEntries.Count,
            FieldChangesRecorded = fieldChanges.Count,
            Warnings = warnings.AsReadOnly()
        };
    }

    private async Task<IngestResult> PersistJsonRowsAsync(
        List<(string TicketKey, string CompanyName, Dictionary<string, string?> Fields)> rows,
        string itsmSourceName,
        Dictionary<string, string> canonicalLookup,
        DateOnly snapshotDate,
        List<string> warnings,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return new IngestResult
            {
                Success = true,
                SnapshotId = Guid.Empty,
                TicketsIngested = 0,
                FieldChangesRecorded = 0,
                Warnings = warnings.AsReadOnly()
            };
        }

        DateTime snapshotDateTime = snapshotDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Create snapshot record via ICommandRepository
        var snapshot = await _commandRepository.CreateSnapshotAsync(
            new Snapshot
            {
                Id           = Guid.NewGuid(),
                ItsmSource   = itsmSourceName,
                SnapshotDate = snapshotDateTime,
                UploadedBy   = "web-upload",
                UploadedAt   = DateTime.UtcNow,
            },
            ct);

        Guid snapshotId = snapshot.Id;

        var ticketEntries = rows
            .Select(r => (TicketKey: r.TicketKey, CompanyName: r.CompanyName))
            .Distinct()
            .ToList();

        var snapshotTickets = ticketEntries
            .Select(e => new SnapshotTicket
            {
                Id          = Guid.NewGuid(),
                SnapshotId  = snapshotId,
                CompanyName = e.CompanyName,
                TicketKey   = e.TicketKey,
            })
            .ToList();

        await _commandRepository.AddSnapshotTicketsAsync(snapshotTickets, ct);

        // Figure out which canonical field names are present in this JSON payload (excluding company).
        var candidateFieldNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, _, fields) in rows)
        {
            foreach (var sourceFieldName in fields.Keys)
            {
                string? canonical = canonicalLookup.TryGetValue(sourceFieldName, out var c) ? c : null;

                if (IsCompanyCanonical(canonical)
                    || IsTicketNumberCanonical(canonical))
                    continue;

                string fieldName = canonical ?? sourceFieldName;
                candidateFieldNames.Add(fieldName);
            }
        }

        var lastKnownValues = await LoadLatestFieldValuesAsync(ticketEntries, candidateFieldNames, snapshotDateTime, ct);

        // Resolve canonical names and batch-build only the deltas
        var fieldChanges = new List<FieldChange>();
        foreach (var (ticketKey, companyName, fields) in rows)
        {
            foreach (var (sourceFieldName, rawValue) in fields)
            {
                string? canonical = canonicalLookup.TryGetValue(sourceFieldName, out var c) ? c : null;

                // Don't store company or ticket identity fields as ticket fields
                if (IsCompanyCanonical(canonical)
                    || IsTicketNumberCanonical(canonical))
                    continue;

                string fieldName = canonical ?? sourceFieldName;

                var fieldValue = NormalizeValue(rawValue);

                var key = (CompanyName: companyName, TicketKey: ticketKey, FieldName: fieldName);

                if (lastKnownValues.TryGetValue(key, out var previousValue))
                {
                    if (string.Equals(previousValue, fieldValue, StringComparison.Ordinal))
                        continue;
                }
                else
                {
                    if (fieldValue is null)
                        continue;
                }

                fieldChanges.Add(new FieldChange
                {
                    CompanyName = companyName,
                    TicketKey   = ticketKey,
                    FieldName   = fieldName,
                    FieldValue  = fieldValue,
                    ObservedAt  = snapshotDateTime,
                    SnapshotId  = snapshotId,
                });

                lastKnownValues[key] = fieldValue;
            }
        }

        await _commandRepository.RecordFieldChangesAsync(fieldChanges, ct);

        string? actor = _httpContextAccessor?.HttpContext?.User?.FindFirst("preferred_username")?.Value
                        ?? _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Upn)?.Value
                        ?? _httpContextAccessor?.HttpContext?.User?.FindFirst("name")?.Value
                        ?? _httpContextAccessor?.HttpContext?.User?.FindFirst("oid")?.Value
                        ?? "anonymous";

        _logger.LogInformation("Snapshot {SnapshotId} ingested by {Actor}: {Tickets} tickets, {Changes} field changes",
            snapshotId, actor, ticketEntries.Count, fieldChanges.Count);

        return new IngestResult
        {
            Success = true,
            SnapshotId = snapshotId,
            TicketsIngested = ticketEntries.Count,
            FieldChangesRecorded = fieldChanges.Count,
            Warnings = warnings.AsReadOnly()
        };
    }

    private static readonly string[] CanonicalAliasesForTicketNumber = ["Number", "ticket_number", "ticket_key"];
    private static readonly string[] CanonicalAliasesForCompany = ["Company", "company"];

    private static int FindCanonicalIndex(string?[] canonicalHeaders, params string[] canonicalNames)
    {
        for (int i = 0; i < canonicalHeaders.Length; i++)
        {
            if (MatchesCanonical(canonicalHeaders[i], canonicalNames))
                return i;
        }
        return -1;
    }

    private static bool IsTicketNumberCanonical(string? canonical) => MatchesCanonical(canonical, CanonicalAliasesForTicketNumber);
    private static bool IsCompanyCanonical(string? canonical) => MatchesCanonical(canonical, CanonicalAliasesForCompany);

    private static bool MatchesCanonical(string? canonical, params string[] canonicalNames)
    {
        if (string.IsNullOrWhiteSpace(canonical))
            return false;

        foreach (var name in canonicalNames)
        {
            if (string.Equals(canonical, name, StringComparison.Ordinal)
                || CanonicalFieldCatalog.GetAliases(name).Any(alias => string.Equals(alias, canonical, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the source field name whose canonical maps to <paramref name="targetCanonical"/>
    /// and returns its value from <paramref name="fields"/>.
    /// </summary>
    private Task<string?> ResolveCanonicalFieldValueAsync(
        Dictionary<string, string?> fields,
        Dictionary<string, string> canonicalLookup,
        string targetCanonical)
    {
        foreach (var (sourceField, value) in fields)
        {
            if (canonicalLookup.TryGetValue(sourceField, out var canonical) &&
                MatchesCanonical(canonical, targetCanonical))
                return Task.FromResult<string?>(value);
        }
        return Task.FromResult<string?>(null);
    }

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private async Task<Dictionary<(string CompanyName, string TicketKey, string FieldName), string?>> LoadLatestFieldValuesAsync(
        IReadOnlyCollection<(string TicketKey, string CompanyName)> ticketEntries,
        IReadOnlyCollection<string> fieldNames,
        DateTime asOfUtc,
        CancellationToken ct)
    {
        if (ticketEntries.Count == 0 || fieldNames.Count == 0)
            return new Dictionary<(string CompanyName, string TicketKey, string FieldName), string?>();

        var ticketKeySet = ticketEntries.Select(t => t.TicketKey).ToHashSet(StringComparer.Ordinal);
        var companySet   = ticketEntries.Select(t => t.CompanyName).ToHashSet(StringComparer.Ordinal);
        var fieldSet     = fieldNames.ToHashSet(StringComparer.Ordinal);

        var rows = await _dbContext.FieldChanges
            .AsNoTracking()
            .Where(fc => ticketKeySet.Contains(fc.TicketKey)
                      && companySet.Contains(fc.CompanyName)
                      && fieldSet.Contains(fc.FieldName)
                      && fc.ObservedAt <= asOfUtc)
            .Select(fc => new { fc.CompanyName, fc.TicketKey, fc.FieldName, fc.FieldValue, fc.ObservedAt })
            .ToListAsync(ct);

        // Compute latest values per (company, ticket, field) in-memory.
        return rows
            .GroupBy(r => (r.CompanyName, r.TicketKey, r.FieldName))
            .Select(g => g.OrderByDescending(r => r.ObservedAt).First())
            .ToDictionary(
                r => (r.CompanyName, r.TicketKey, r.FieldName),
                r => NormalizeValue(r.FieldValue));
    }

}
