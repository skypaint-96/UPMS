namespace UPMS.Web.Services;

using System.Text;
using System.Text.Json;
using UPMS.Data;

/// <summary>
/// Parses uploaded snapshot files and persists extracted ticket and field change data.
/// Supports CSV and JSON formats. Field names are mapped to canonical names via IItsmFieldMappingService.
/// </summary>
public class SnapshotIngestService : ISnapshotIngestService
{
    private readonly TicketDataServiceInstance _dataService;
    private readonly IItsmFieldMappingService _mappingService;

    public SnapshotIngestService(
        TicketDataServiceInstance dataService,
        IItsmFieldMappingService mappingService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
    }

    /// <inheritdoc />
    public async Task<IngestResult> IngestCsvAsync(
        Stream fileStream,
        string itsmSource,
        DateTime snapshotDate,
        string uploadedBy,
        string companyName,
        CancellationToken ct = default)
    {
        try
        {
            using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
            var warnings = new List<string>();

            // Read header line
            string? headerLine = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(headerLine))
            {
                return IngestResult.Failure("CSV file is empty or has no header row.");
            }

            // Parse headers case-insensitively
            string[] headers = headerLine.Split(',');
            int ticketKeyIndex = -1;
            int fieldNameIndex = -1;
            int fieldValueIndex = -1;

            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i].Trim();
                if (header.Equals("ticket_key", StringComparison.OrdinalIgnoreCase))
                    ticketKeyIndex = i;
                else if (header.Equals("field_name", StringComparison.OrdinalIgnoreCase))
                    fieldNameIndex = i;
                else if (header.Equals("field_value", StringComparison.OrdinalIgnoreCase))
                    fieldValueIndex = i;
            }

            if (ticketKeyIndex < 0 || fieldNameIndex < 0 || fieldValueIndex < 0)
            {
                return IngestResult.Failure("CSV is missing required columns: ticket_key, field_name, field_value");
            }

            // Parse data rows
            var rows = new List<(string TicketKey, string FieldName, string? FieldValue)>();
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

                string[] columns = line.Split(',');

                if (columns.Length <= Math.Max(ticketKeyIndex, Math.Max(fieldNameIndex, fieldValueIndex)))
                {
                    warnings.Add($"Line {lineNumber}: insufficient columns, row skipped.");
                    continue;
                }

                string ticketKey = columns[ticketKeyIndex].Trim();
                string fieldName = columns[fieldNameIndex].Trim();
                string fieldValue = columns[fieldValueIndex].Trim();

                if (string.IsNullOrWhiteSpace(ticketKey) || string.IsNullOrWhiteSpace(fieldName))
                {
                    warnings.Add($"Line {lineNumber}: empty ticket_key or field_name, row skipped.");
                    continue;
                }

                rows.Add((ticketKey, fieldName, string.IsNullOrEmpty(fieldValue) ? null : fieldValue));
            }

            return await PersistIngestDataAsync(
                rows, itsmSource, snapshotDate, uploadedBy, companyName, warnings, ct);
        }
        catch (Exception ex)
        {
            return IngestResult.Failure(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<IngestResult> IngestJsonAsync(
        Stream fileStream,
        string itsmSource,
        DateTime snapshotDate,
        string uploadedBy,
        string companyName,
        CancellationToken ct = default)
    {
        try
        {
            var warnings = new List<string>();
            var rows = new List<(string TicketKey, string FieldName, string? FieldValue)>();

            using JsonDocument document = await JsonDocument.ParseAsync(fileStream, cancellationToken: ct);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return IngestResult.Failure("JSON root element must be an array.");
            }

            int elementIndex = 0;
            foreach (JsonElement element in document.RootElement.EnumerateArray())
            {
                elementIndex++;

                if (!element.TryGetProperty("ticket_key", out JsonElement ticketKeyElement))
                {
                    warnings.Add($"Element {elementIndex}: missing 'ticket_key' property, skipped.");
                    continue;
                }

                string? ticketKey = ticketKeyElement.GetString();
                if (string.IsNullOrWhiteSpace(ticketKey))
                {
                    warnings.Add($"Element {elementIndex}: empty ticket_key, skipped.");
                    continue;
                }

                if (!element.TryGetProperty("fields", out JsonElement fieldsElement)
                    || fieldsElement.ValueKind != JsonValueKind.Object)
                {
                    warnings.Add($"Element {elementIndex} (ticket '{ticketKey}'): missing or invalid 'fields' object, skipped.");
                    continue;
                }

                foreach (JsonProperty field in fieldsElement.EnumerateObject())
                {
                    string fieldName = field.Name;
                    string? fieldValue = field.Value.ValueKind == JsonValueKind.Null
                        ? null
                        : field.Value.GetString();

                    if (string.IsNullOrWhiteSpace(fieldName))
                    {
                        warnings.Add($"Element {elementIndex} (ticket '{ticketKey}'): empty field name, skipped.");
                        continue;
                    }

                    rows.Add((ticketKey, fieldName, fieldValue));
                }
            }

            return await PersistIngestDataAsync(
                rows, itsmSource, snapshotDate, uploadedBy, companyName, warnings, ct);
        }
        catch (JsonException ex)
        {
            return IngestResult.Failure($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            return IngestResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Persists parsed rows: creates a snapshot, registers tickets, and records all field changes.
    /// </summary>
    private async Task<IngestResult> PersistIngestDataAsync(
        List<(string TicketKey, string FieldName, string? FieldValue)> rows,
        string itsmSource,
        DateTime snapshotDate,
        string uploadedBy,
        string companyName,
        List<string> warnings,
        CancellationToken ct)
    {
        _ = ct; // cancellation propagation to Dapper not supported; parameter reserved

        // Create the snapshot record
        Guid snapshotId = await _dataService.CreateSnapshotAsync(
            itsmSource, snapshotDate, uploadedBy);

        // Collect distinct ticket keys
        var distinctTickets = rows
            .Select(r => r.TicketKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(key => (TicketKey: key, CompanyName: companyName))
            .ToList();

        if (distinctTickets.Count > 0)
        {
            await _dataService.AddTicketsToSnapshotAsync(snapshotId, distinctTickets);
        }

        // Record each field change with canonical field name lookup
        int fieldChangesRecorded = 0;
        foreach ((string ticketKey, string fieldName, string? fieldValue) in rows)
        {
            string canonicalName = _mappingService.GetCanonicalName(itsmSource, fieldName);

            await _dataService.RecordFieldChangeAsync(
                companyName,
                ticketKey,
                canonicalName,
                fieldValue,
                snapshotDate,
                snapshotId);

            fieldChangesRecorded++;
        }

        return new IngestResult
        {
            Success = true,
            SnapshotId = snapshotId,
            TicketsIngested = distinctTickets.Count,
            FieldChangesRecorded = fieldChangesRecorded,
            Warnings = warnings.AsReadOnly()
        };
    }
}
