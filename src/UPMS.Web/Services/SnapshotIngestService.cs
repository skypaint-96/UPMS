namespace UPMS.Web.Services;

using System.Text;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UPMS.Data;

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
    private readonly ILogger<SnapshotIngestService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public SnapshotIngestService(
        ICommandRepository commandRepository,
        IItsmSourceService sourceService,
        ILogger<SnapshotIngestService> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _commandRepository = commandRepository ?? throw new ArgumentNullException(nameof(commandRepository));
        _sourceService     = sourceService     ?? throw new ArgumentNullException(nameof(sourceService));
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

            // Locate the column index for the canonical "ticket_key" and "company" fields
            int ticketKeyColIndex = FindCanonicalIndex(canonicalHeaders, "ticket_key");
            int companyColIndex   = FindCanonicalIndex(canonicalHeaders, "company");

            if (ticketKeyColIndex < 0)
            {
                return IngestResult.Failure(
                    "No column is mapped to canonical name 'ticket_key' for this ITSM source. " +
                    "Add a field mapping for ticket_key before uploading.");
            }

            if (companyColIndex < 0)
            {
                return IngestResult.Failure(
                    "No column is mapped to canonical name 'company' for this ITSM source. " +
                    "Add a field mapping for company before uploading.");
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

                if (cols.Length <= Math.Max(ticketKeyColIndex, companyColIndex))
                {
                    warnings.Add($"Line {lineNumber}: insufficient columns, row skipped.");
                    continue;
                }

                string ticketKey  = cols[ticketKeyColIndex];
                string companyName = cols[companyColIndex];

                if (string.IsNullOrWhiteSpace(ticketKey))
                {
                    warnings.Add($"Line {lineNumber}: empty ticket key, row skipped.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(companyName))
                {
                    warnings.Add($"Line {lineNumber}: empty company value, row skipped.");
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

                // Resolve ticket key: find the field whose canonical name is "ticket_key"
                string? ticketKey  = await ResolveCanonicalFieldValueAsync(itsmSourceName, fields, "ticket_key");
                string? companyName = await ResolveCanonicalFieldValueAsync(itsmSourceName, fields, "company");

                if (string.IsNullOrWhiteSpace(ticketKey))
                {
                    warnings.Add($"Element {elementIndex}: could not resolve ticket_key value, skipped.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(companyName))
                {
                    warnings.Add($"Element {elementIndex}: could not resolve company value, skipped.");
                    continue;
                }

                parsedRows.Add((ticketKey, companyName, fields));
            }

            return await PersistJsonRowsAsync(
                parsedRows, itsmSourceName, snapshotDate, warnings, ct);
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

        // Batch-build and persist field changes
        var fieldChanges = new List<FieldChange>();
        foreach (var (ticketKey, companyName, cols) in rows)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                string fieldName = canonicalHeaders[i] ?? headers[i];
                string? fieldValue = i < cols.Length
                    ? (string.IsNullOrEmpty(cols[i]) ? null : cols[i])
                    : null;

                fieldChanges.Add(new FieldChange
                {
                    CompanyName = companyName,
                    TicketKey   = ticketKey,
                    FieldName   = fieldName,
                    FieldValue  = fieldValue,
                    ObservedAt  = snapshotDateTime,
                    SnapshotId  = snapshotId,
                });
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

        // Resolve canonical names and batch-build field changes
        var fieldChanges = new List<FieldChange>();
        foreach (var (ticketKey, companyName, fields) in rows)
        {
            foreach (var (sourceFieldName, fieldValue) in fields)
            {
                string? canonical = await _sourceService.GetCanonicalNameAsync(itsmSourceName, sourceFieldName);
                string fieldName = canonical ?? sourceFieldName;

                fieldChanges.Add(new FieldChange
                {
                    CompanyName = companyName,
                    TicketKey   = ticketKey,
                    FieldName   = fieldName,
                    FieldValue  = fieldValue,
                    ObservedAt  = snapshotDateTime,
                    SnapshotId  = snapshotId,
                });
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

    private static int FindCanonicalIndex(string?[] canonicalHeaders, string canonicalName)
    {
        for (int i = 0; i < canonicalHeaders.Length; i++)
        {
            if (string.Equals(canonicalHeaders[i], canonicalName, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Finds the source field name whose canonical maps to <paramref name="targetCanonical"/>
    /// and returns its value from <paramref name="fields"/>.
    /// </summary>
    private async Task<string?> ResolveCanonicalFieldValueAsync(
        string sourceName,
        Dictionary<string, string?> fields,
        string targetCanonical)
    {
        foreach (var (sourceField, value) in fields)
        {
            string? canonical = await _sourceService.GetCanonicalNameAsync(sourceName, sourceField);
            if (string.Equals(canonical, targetCanonical, StringComparison.Ordinal))
                return value;
        }
        return null;
    }
}
