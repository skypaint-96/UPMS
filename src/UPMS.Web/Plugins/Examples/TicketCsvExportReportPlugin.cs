namespace UPMS.Web.Plugins.Examples;

using System.Text;
using UPMS.Data;
using UPMS.Web.Reporting;

/// <summary>
/// Example reporting plugin that exports tickets as-of a selected date as a CSV download.
/// </summary>
public sealed class TicketCsvExportReportPlugin : IReportPlugin
{
    private readonly TicketDataServiceInstance _dataService;

    public TicketCsvExportReportPlugin(TicketDataServiceInstance dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public string PluginId => "example-ticket-csv-export";
    public string DisplayName => "Ticket CSV Export (Example)";
    public string Description => "Exports tickets for a company/source as-of a date to a CSV file.";

    public IReadOnlyList<ReportParameterDefinition> Parameters =>
    [
        new ReportParameterDefinition
        {
            Key = "itsm_source",
            DisplayName = "ITSM Source",
            Type = ReportParameterType.ItsmSource,
            IsRequired = true
        },
        new ReportParameterDefinition
        {
            Key = "company",
            DisplayName = "Company",
            Type = ReportParameterType.Text,
            IsRequired = true
        },
        new ReportParameterDefinition
        {
            Key = "as_of_date",
            DisplayName = "As Of Date",
            Type = ReportParameterType.Date,
            IsRequired = true
        },
        new ReportParameterDefinition
        {
            Key = "fields",
            DisplayName = "Fields to Include",
            Type = ReportParameterType.MultiSelect,
            IsRequired = false,
            Description = "Optional: choose which fields to include as extra CSV columns.",
            Options = [
                "State",
                "Priority",
                "Assigned To",
                "Assignment Group",
                "Short Description",
                "Description",
                "Category",
                "Subcategory",
                "Business Service",
                "Service Offering",
                "Opened At",
                "Created On",
                "Updated On",
                "Resolved At",
                "Closed At"
            ]
        }
    ];

    public async Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        if (!request.Parameters.TryGetValue("itsm_source", out var itsmSource) || string.IsNullOrWhiteSpace(itsmSource))
            return ReportResult.Failure("Required parameter 'itsm_source' is missing.");

        if (!request.Parameters.TryGetValue("company", out var company) || string.IsNullOrWhiteSpace(company))
            return ReportResult.Failure("Required parameter 'company' is missing.");

        if (!request.Parameters.TryGetValue("as_of_date", out var asOfDateStr) || !DateTime.TryParse(asOfDateStr, out var asOfDate))
            return ReportResult.Failure("Required parameter 'as_of_date' is missing or invalid.");

        // MultiSelect values are stored as a comma-separated string by ReportParameterForm.
        request.Parameters.TryGetValue("fields", out var fieldsRaw);
        var selectedFields = ParseMulti(fieldsRaw);

        try
        {
            var tickets = (await _dataService.GetTicketsAsync(itsmSource, company, asOfDate)).ToList();

            // Base columns are always included.
            var columns = new List<string>
            {
                "ticket_key",
                "ticket_number",
                "company",
                "itsm_source",
                "snapshot_date",
                "observed_at"
            };

            foreach (var field in selectedFields)
            {
                if (!columns.Contains(field, StringComparer.OrdinalIgnoreCase))
                    columns.Add(field);
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(',', columns.Select(EscapeCsv)));

            foreach (var ticket in tickets)
            {
                var row = new List<string>(columns.Count);
                foreach (var col in columns)
                {
                    row.Add(EscapeCsv(GetColumnValue(ticket, col)));
                }

                sb.AppendLine(string.Join(',', row));
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"UPMS_Tickets_{SanitizeFilePart(company)}_{asOfDate:yyyy-MM-dd}.csv";

            return new ReportResult
            {
                Success = true,
                OutputType = ReportOutputType.FileDownload,
                FileName = fileName,
                ContentType = "text/csv",
                FileContent = bytes
            };
        }
        catch (Exception ex)
        {
            return ReportResult.Failure($"Failed to export CSV: {ex.Message}");
        }
    }

    private static string GetColumnValue(Ticket ticket, string column)
    {
        // System columns
        if (string.Equals(column, "ticket_key", StringComparison.OrdinalIgnoreCase))
            return ticket.TicketKey;

        if (string.Equals(column, "ticket_number", StringComparison.OrdinalIgnoreCase))
        {
            var number = TicketFieldHelpers.GetFieldValue(ticket, "Number", fallback: string.Empty);
            if (!string.IsNullOrWhiteSpace(number))
                return number;

            return ticket.TicketKey;
        }

        if (string.Equals(column, "company", StringComparison.OrdinalIgnoreCase))
            return ticket.CompanyName;

        if (string.Equals(column, "itsm_source", StringComparison.OrdinalIgnoreCase))
            return ticket.ItsmSource;

        if (string.Equals(column, "snapshot_date", StringComparison.OrdinalIgnoreCase))
            return ticket.SnapshotDate == default ? string.Empty : ticket.SnapshotDate.ToString("o");

        if (string.Equals(column, "observed_at", StringComparison.OrdinalIgnoreCase))
            return ticket.ObservedAt == default ? string.Empty : ticket.ObservedAt.ToString("o");

        // Ticket fields (canonical + legacy aliases)
        var fieldValue = TicketFieldHelpers.GetFieldValue(ticket, column, fallback: string.Empty);
        if (!string.IsNullOrWhiteSpace(fieldValue))
            return fieldValue;

        return string.Empty;
    }

    private static bool TryGetField(IDictionary<string, string?> fields, string fieldName, out string? value)
    {
        if (fields.TryGetValue(fieldName, out value))
            return true;

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

    private static IReadOnlyList<string> ParseMulti(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;

        // RFC 4180 style quoting.
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            var escaped = value.Replace("\"", "\"\"");
            return $"\"{escaped}\"";
        }

        return value;
    }

    private static string SanitizeFilePart(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
                sb.Append(ch);
            else if (char.IsWhiteSpace(ch))
                sb.Append('_');
        }

        return sb.Length == 0 ? "company" : sb.ToString();
    }
}
