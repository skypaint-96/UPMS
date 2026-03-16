namespace UPMS.Ingestion;

using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;
using UPMS.Data;

/// <summary>
/// Parses uploaded flat-table CSV and JSON snapshot files and persists extracted ticket and
/// field-change data. Field names are mapped to canonical names via <see cref="IItsmSourceService"/>.
/// Company is read from the CSV/JSON row data (the column mapped to canonical "Company") and is
/// not passed as a parameter.
/// </summary>
public class SnapshotIngestService : ISnapshotIngestService
{
    private readonly ICommandRepository _commandRepository;
    private readonly IItsmSourceService _sourceService;
    private readonly UpmsDbContext _dbContext;
    private readonly ILogger<SnapshotIngestService> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ISnapshotDuplicateDetector _duplicateDetector;

    public SnapshotIngestService(
        ICommandRepository commandRepository,
        IItsmSourceService sourceService,
        UpmsDbContext dbContext,
        ILogger<SnapshotIngestService> logger,
        ISnapshotDuplicateDetector duplicateDetector,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _commandRepository = commandRepository ?? throw new ArgumentNullException(nameof(commandRepository));
        _sourceService = sourceService ?? throw new ArgumentNullException(nameof(sourceService));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _duplicateDetector = duplicateDetector ?? throw new ArgumentNullException(nameof(duplicateDetector));
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public async Task<IngestResult> IngestCsvAsync(
        Stream csvStream,
        string itsmSourceName,
        DateOnly snapshotDate,
        SnapshotIngestMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(csvStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSourceName);

        var normalizedMetadata = SnapshotIngestMetadataHelper.Normalize(
            metadata,
            metadata?.OriginalFileName ?? "snapshot.csv",
            metadata?.ContentType ?? "text/csv",
            metadata?.UploadChannel ?? SnapshotUploadChannels.ManualSync,
            metadata?.IsAutomated ?? false,
            metadata?.ArtifactPath,
            metadata?.ContentSha256,
            ResolveRequestedByFromContext());

        try
        {
            var sourceDefinition = await _sourceService.GetSourceDefinitionAsync(itsmSourceName);
            IReadOnlyList<string> requiredFields = await _sourceService.GetRequiredFieldsAsync(itsmSourceName);
            var mappingLookup = sourceDefinition.Mappings
                .GroupBy(m => m.SourceFieldName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().CanonicalFieldName, StringComparer.OrdinalIgnoreCase);

            using var reader = new StreamReader(
                csvStream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true);
            using var parser = new TextFieldParser(reader)
            {
                TextFieldType = FieldType.Delimited,
                HasFieldsEnclosedInQuotes = true,
                TrimWhiteSpace = false
            };
            parser.SetDelimiters(",");

            if (parser.EndOfData)
                return IngestResult.Failure("CSV file is empty or has no header row.");

            string[] headerFields;
            try
            {
                headerFields = parser.ReadFields() ?? Array.Empty<string>();
            }
            catch (MalformedLineException ex)
            {
                return IngestResult.Failure($"Invalid CSV header row: {ex.Message}");
            }

            string[] headers = headerFields
                .Select(field => field?.Trim() ?? string.Empty)
                .ToArray();

            if (headers.Length == 0 || headers.All(string.IsNullOrWhiteSpace))
                return IngestResult.Failure("CSV file is empty or has no header row.");

            var duplicateHeaders = headers
                .Where(header => !string.IsNullOrWhiteSpace(header))
                .GroupBy(header => header, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (duplicateHeaders.Length > 0)
            {
                return IngestResult.Failure($"Duplicate CSV header names are not allowed: {string.Join(", ", duplicateHeaders)}");
            }

            var missingRequired = requiredFields
                .Where(required => !headers.Contains(required, StringComparer.OrdinalIgnoreCase))
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missingRequired.Count > 0)
            {
                return IngestResult.Failure($"Missing required fields: {string.Join(", ", missingRequired)}");
            }

            string?[] canonicalHeaders = headers
                .Select(header => mappingLookup.TryGetValue(header, out var canonical) ? canonical : null)
                .ToArray();

            int ticketNumberColIndex = FindCanonicalIndex(canonicalHeaders, CanonicalAliasesForTicketNumber);
            int companyColIndex = FindCanonicalIndex(canonicalHeaders, CanonicalAliasesForCompany);

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

            var warnings = new List<string>();
            var parsedRows = new List<(string TicketKey, string CompanyName, string[] Columns)>();
            var recordNumber = 0;

            while (!parser.EndOfData)
            {
                ct.ThrowIfCancellationRequested();
                recordNumber++;

                string[] cols;
                try
                {
                    cols = parser.ReadFields() ?? Array.Empty<string>();
                }
                catch (MalformedLineException ex)
                {
                    return IngestResult.Failure($"Invalid CSV data near line {ResolveCsvErrorLineNumber(parser, recordNumber + 1)}: {ex.Message}", warnings.AsReadOnly());
                }

                if (IsBlankRecord(cols))
                {
                    warnings.Add($"Record {recordNumber}: blank row skipped.");
                    continue;
                }

                var normalizedColumns = cols.Select(column => column?.Trim() ?? string.Empty).ToArray();
                if (normalizedColumns.Length <= Math.Max(ticketNumberColIndex, companyColIndex))
                {
                    warnings.Add($"Record {recordNumber}: insufficient columns, row skipped.");
                    continue;
                }

                string? ticketNumber = NormalizeValue(normalizedColumns[ticketNumberColIndex]);
                string? companyName = NormalizeValue(normalizedColumns[companyColIndex]);

                if (string.IsNullOrWhiteSpace(ticketNumber))
                {
                    warnings.Add($"Record {recordNumber}: empty ticket number, row skipped.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(companyName))
                {
                    warnings.Add($"Record {recordNumber}: empty company value, row skipped.");
                    continue;
                }

                string ticketKey;
                try
                {
                    ticketKey = TicketKeyFactory.Compose(itsmSourceName, companyName, ticketNumber);
                }
                catch (ArgumentException ex)
                {
                    warnings.Add($"Record {recordNumber}: {ex.Message}, row skipped.");
                    continue;
                }

                parsedRows.Add((ticketKey, companyName, normalizedColumns));
            }

            return await PersistFlatTableRowsAsync(
                parsedRows,
                headers,
                canonicalHeaders,
                itsmSourceName,
                snapshotDate,
                normalizedMetadata,
                warnings,
                ct);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Snapshot ingest rejected because ITSM source {Source} was not found.", itsmSourceName);
            return IngestResult.Failure(ex.Message);
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
        SnapshotIngestMetadata? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jsonStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSourceName);

        var normalizedMetadata = SnapshotIngestMetadataHelper.Normalize(
            metadata,
            metadata?.OriginalFileName ?? "snapshot.json",
            metadata?.ContentType ?? "application/json",
            metadata?.UploadChannel ?? SnapshotUploadChannels.ManualSync,
            metadata?.IsAutomated ?? false,
            metadata?.ArtifactPath,
            metadata?.ContentSha256,
            ResolveRequestedByFromContext());

        try
        {
            var warnings = new List<string>();
            var parsedRows = new List<(string TicketKey, string CompanyName, Dictionary<string, string?> Fields)>();

            var sourceDefinition = await _sourceService.GetSourceDefinitionAsync(itsmSourceName);
            IReadOnlyList<string> requiredFields = await _sourceService.GetRequiredFieldsAsync(itsmSourceName);
            var canonicalLookup = sourceDefinition.Mappings
                .GroupBy(m => m.SourceFieldName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().CanonicalFieldName, StringComparer.OrdinalIgnoreCase);

            using JsonDocument document = await JsonDocument.ParseAsync(jsonStream, new JsonDocumentOptions(), ct);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return IngestResult.Failure("JSON root element must be an array.");
            }

            int elementIndex = 0;
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                elementIndex++;

                if (element.ValueKind != JsonValueKind.Object)
                {
                    warnings.Add($"Element {elementIndex}: not a JSON object, skipped.");
                    continue;
                }

                var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    fields[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : prop.Value.ToString();
                }

                var missingRequired = requiredFields
                    .Where(required => !fields.ContainsKey(required))
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (missingRequired.Count > 0)
                {
                    warnings.Add($"Element {elementIndex}: missing required fields: {string.Join(", ", missingRequired)}, skipped.");
                    continue;
                }

                string? ticketNumber = null;
                string? companyName = null;
                foreach (var (sourceField, value) in fields)
                {
                    if (ticketNumber is null && canonicalLookup.TryGetValue(sourceField, out var ticketCanonical) && IsTicketNumberCanonical(ticketCanonical))
                        ticketNumber = value;

                    if (companyName is null && canonicalLookup.TryGetValue(sourceField, out var companyCanonical) && IsCompanyCanonical(companyCanonical))
                        companyName = value;

                    if (ticketNumber is not null && companyName is not null)
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
                parsedRows,
                itsmSourceName,
                canonicalLookup,
                snapshotDate,
                normalizedMetadata,
                warnings,
                ct);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Snapshot ingest rejected because ITSM source {Source} was not found.", itsmSourceName);
            return IngestResult.Failure(ex.Message);
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

    private async Task<IngestResult> PersistFlatTableRowsAsync(
        List<(string TicketKey, string CompanyName, string[] Columns)> rows,
        string[] headers,
        string?[] canonicalHeaders,
        string itsmSourceName,
        DateOnly snapshotDate,
        SnapshotIngestMetadata metadata,
        List<string> warnings,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return IngestResult.Failure(
                warnings.Count == 0
                    ? "CSV file contained no data rows."
                    : "No valid rows were found in the uploaded CSV file.",
                warnings.AsReadOnly());
        }

        var duplicate = await _duplicateDetector.FindExistingSnapshotAsync(itsmSourceName, snapshotDate, metadata, ct);
        if (duplicate is not null)
        {
            warnings.Add($"Duplicate snapshot ignored: {duplicate.Reason}");
            _logger.LogInformation(
                "Duplicate snapshot upload detected for source {Source} on {SnapshotDate}: existing snapshot {SnapshotId}.",
                itsmSourceName,
                snapshotDate,
                duplicate.SnapshotId);
            return IngestResult.Duplicate(duplicate.SnapshotId, duplicate.Reason, warnings.AsReadOnly());
        }

        DateTime snapshotDateTime = snapshotDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var snapshot = await _commandRepository.CreateSnapshotAsync(
            new Snapshot
            {
                Id = Guid.NewGuid(),
                ItsmSource = itsmSourceName,
                SnapshotDate = snapshotDateTime,
                UploadedBy = ResolveUploadedBy(metadata),
                UploadedAt = metadata.SubmittedAtUtc,
                UploadMetadata = SnapshotIngestMetadataHelper.Serialize(metadata)
            },
            ct);

        Guid snapshotId = snapshot.Id;

        int ticketNumberColIndex = FindCanonicalIndex(canonicalHeaders, CanonicalAliasesForTicketNumber);
        int companyColIndex = FindCanonicalIndex(canonicalHeaders, CanonicalAliasesForCompany);

        var ticketEntries = rows
            .Select(r => (TicketKey: r.TicketKey, CompanyName: r.CompanyName))
            .Distinct()
            .ToList();

        var snapshotTickets = ticketEntries
            .Select(e => new SnapshotTicket
            {
                Id = Guid.NewGuid(),
                SnapshotId = snapshotId,
                CompanyName = e.CompanyName,
                TicketKey = e.TicketKey,
            })
            .ToList();

        await _commandRepository.AddSnapshotTicketsAsync(snapshotTickets, ct);

        var candidateFieldNames = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < headers.Length; i++)
        {
            if (i == companyColIndex)
                continue;

            string? canonical = canonicalHeaders[i];
            if (i == ticketNumberColIndex || IsTicketNumberCanonical(canonical))
                continue;

            string displayFieldName = GetDisplayFieldName(canonical, headers[i]);
            candidateFieldNames.Add(displayFieldName);
        }

        var lastKnownValues = await LoadLatestFieldValuesAsync(ticketEntries, candidateFieldNames, snapshotDateTime, ct);

        var fieldChanges = new List<FieldChange>();
        foreach (var (ticketKey, companyName, cols) in rows)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (i == companyColIndex)
                    continue;

                string? canonical = canonicalHeaders[i];
                if (i == ticketNumberColIndex || IsTicketNumberCanonical(canonical))
                    continue;

                string sourceFieldName = headers[i];
                string displayFieldName = GetDisplayFieldName(canonical, sourceFieldName);
                string? fieldValue = i < cols.Length ? NormalizeValue(cols[i]) : null;

                var key = (CompanyName: companyName, TicketKey: ticketKey, FieldName: displayFieldName);

                if (lastKnownValues.TryGetValue(key, out var previousValue))
                {
                    if (string.Equals(previousValue, fieldValue, StringComparison.Ordinal))
                        continue;
                }
                else if (fieldValue is null)
                {
                    continue;
                }

                fieldChanges.Add(new FieldChange
                {
                    CompanyName = companyName,
                    TicketKey = ticketKey,
                    FieldName = sourceFieldName,
                    CanonicalFieldName = string.IsNullOrWhiteSpace(canonical) ? null : canonical.Trim(),
                    FieldValue = fieldValue,
                    ObservedAt = snapshotDateTime,
                    SnapshotId = snapshotId,
                });

                lastKnownValues[key] = fieldValue;
            }
        }

        await _commandRepository.RecordFieldChangesAsync(fieldChanges, ct);

        var actor = ResolveUploadedBy(metadata);
        _logger.LogInformation(
            "Snapshot {SnapshotId} ingested by {Actor}: {Tickets} tickets, {Changes} field changes, sourceSystem={SourceSystem}, producer={Producer}, channel={UploadChannel}, correlationId={CorrelationId}",
            snapshotId,
            actor,
            ticketEntries.Count,
            fieldChanges.Count,
            metadata.SourceSystem,
            metadata.Producer,
            metadata.UploadChannel,
            metadata.CorrelationId);

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
        SnapshotIngestMetadata metadata,
        List<string> warnings,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return IngestResult.Failure(
                warnings.Count == 0
                    ? "JSON file contained no data elements."
                    : "No valid rows were found in the uploaded JSON file.",
                warnings.AsReadOnly());
        }

        var duplicate = await _duplicateDetector.FindExistingSnapshotAsync(itsmSourceName, snapshotDate, metadata, ct);
        if (duplicate is not null)
        {
            warnings.Add($"Duplicate snapshot ignored: {duplicate.Reason}");
            _logger.LogInformation(
                "Duplicate snapshot upload detected for source {Source} on {SnapshotDate}: existing snapshot {SnapshotId}.",
                itsmSourceName,
                snapshotDate,
                duplicate.SnapshotId);
            return IngestResult.Duplicate(duplicate.SnapshotId, duplicate.Reason, warnings.AsReadOnly());
        }

        DateTime snapshotDateTime = snapshotDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var snapshot = await _commandRepository.CreateSnapshotAsync(
            new Snapshot
            {
                Id = Guid.NewGuid(),
                ItsmSource = itsmSourceName,
                SnapshotDate = snapshotDateTime,
                UploadedBy = ResolveUploadedBy(metadata),
                UploadedAt = metadata.SubmittedAtUtc,
                UploadMetadata = SnapshotIngestMetadataHelper.Serialize(metadata)
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
                Id = Guid.NewGuid(),
                SnapshotId = snapshotId,
                CompanyName = e.CompanyName,
                TicketKey = e.TicketKey,
            })
            .ToList();

        await _commandRepository.AddSnapshotTicketsAsync(snapshotTickets, ct);

        var candidateFieldNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, _, fields) in rows)
        {
            foreach (var sourceFieldName in fields.Keys)
            {
                string? canonical = canonicalLookup.TryGetValue(sourceFieldName, out var c) ? c : null;
                if (IsCompanyCanonical(canonical) || IsTicketNumberCanonical(canonical))
                    continue;

                string displayFieldName = GetDisplayFieldName(canonical, sourceFieldName);
                candidateFieldNames.Add(displayFieldName);
            }
        }

        var lastKnownValues = await LoadLatestFieldValuesAsync(ticketEntries, candidateFieldNames, snapshotDateTime, ct);

        var fieldChanges = new List<FieldChange>();
        foreach (var (ticketKey, companyName, fields) in rows)
        {
            foreach (var (sourceFieldName, rawValue) in fields)
            {
                string? canonical = canonicalLookup.TryGetValue(sourceFieldName, out var c) ? c : null;
                if (IsCompanyCanonical(canonical) || IsTicketNumberCanonical(canonical))
                    continue;

                string displayFieldName = GetDisplayFieldName(canonical, sourceFieldName);
                string? fieldValue = NormalizeValue(rawValue);
                var key = (CompanyName: companyName, TicketKey: ticketKey, FieldName: displayFieldName);

                if (lastKnownValues.TryGetValue(key, out var previousValue))
                {
                    if (string.Equals(previousValue, fieldValue, StringComparison.Ordinal))
                        continue;
                }
                else if (fieldValue is null)
                {
                    continue;
                }

                fieldChanges.Add(new FieldChange
                {
                    CompanyName = companyName,
                    TicketKey = ticketKey,
                    FieldName = sourceFieldName,
                    CanonicalFieldName = string.IsNullOrWhiteSpace(canonical) ? null : canonical.Trim(),
                    FieldValue = fieldValue,
                    ObservedAt = snapshotDateTime,
                    SnapshotId = snapshotId,
                });

                lastKnownValues[key] = fieldValue;
            }
        }

        await _commandRepository.RecordFieldChangesAsync(fieldChanges, ct);

        var actor = ResolveUploadedBy(metadata);
        _logger.LogInformation(
            "Snapshot {SnapshotId} ingested by {Actor}: {Tickets} tickets, {Changes} field changes, sourceSystem={SourceSystem}, producer={Producer}, channel={UploadChannel}, correlationId={CorrelationId}",
            snapshotId,
            actor,
            ticketEntries.Count,
            fieldChanges.Count,
            metadata.SourceSystem,
            metadata.Producer,
            metadata.UploadChannel,
            metadata.CorrelationId);

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

    private static bool IsBlankRecord(string[] values)
    {
        if (values.Length == 0)
            return true;

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return false;
        }

        return true;
    }

    private static long ResolveCsvErrorLineNumber(TextFieldParser parser, int fallbackLineNumber)
    {
        return parser.ErrorLineNumber > 0 ? parser.ErrorLineNumber : fallbackLineNumber;
    }

    private static string GetDisplayFieldName(string? canonicalFieldName, string sourceFieldName)
    {
        if (!string.IsNullOrWhiteSpace(canonicalFieldName))
            return canonicalFieldName.Trim();

        return sourceFieldName?.Trim() ?? string.Empty;
    }

    private static string? NormalizeValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }

    private string ResolveRequestedByFromContext()
    {
        return _httpContextAccessor?.HttpContext?.User?.FindFirst("preferred_username")?.Value
            ?? _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.Upn)?.Value
            ?? _httpContextAccessor?.HttpContext?.User?.FindFirst("name")?.Value
            ?? _httpContextAccessor?.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";
    }

    private string ResolveUploadedBy(SnapshotIngestMetadata metadata)
    {
        return SnapshotIngestMetadataHelper.NormalizeOptional(metadata.RequestedBy)
            ?? SnapshotIngestMetadataHelper.NormalizeOptional(metadata.Producer)
            ?? SnapshotIngestMetadataHelper.NormalizeOptional(metadata.SourceSystem)
            ?? ResolveRequestedByFromContext();
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
        var companySet = ticketEntries.Select(t => t.CompanyName).ToHashSet(StringComparer.Ordinal);
        var displayFieldSet = fieldNames.ToHashSet(StringComparer.Ordinal);

        var rows = await _dbContext.FieldChanges
            .AsNoTracking()
            .Where(fc => ticketKeySet.Contains(fc.TicketKey)
                      && companySet.Contains(fc.CompanyName)
                      && fc.ObservedAt <= asOfUtc
                      && ((fc.CanonicalFieldName != null && displayFieldSet.Contains(fc.CanonicalFieldName))
                          || (fc.CanonicalFieldName == null && displayFieldSet.Contains(fc.FieldName))))
            .Select(fc => new
            {
                fc.Id,
                fc.CompanyName,
                fc.TicketKey,
                fc.FieldName,
                fc.CanonicalFieldName,
                fc.FieldValue,
                fc.ObservedAt
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => (r.CompanyName, r.TicketKey, FieldName: GetDisplayFieldName(r.CanonicalFieldName, r.FieldName)))
            .Select(g => g
                .OrderByDescending(r => r.ObservedAt)
                .ThenByDescending(r => r.Id)
                .First())
            .ToDictionary(
                r => (r.CompanyName, r.TicketKey, GetDisplayFieldName(r.CanonicalFieldName, r.FieldName)),
                r => NormalizeValue(r.FieldValue));
    }
}
