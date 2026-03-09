namespace UPMS.Reporting.Plugins.Documents;

using System.Text;
using UPMS.Data;
using UPMS.Reporting;

/// <summary>
/// Exports a single ticket or a selected set of tickets as DOCX or PDF.
/// The document includes lifecycle fields and optional field-change history.
/// </summary>
public sealed class TicketDocumentExportPlugin : IReportPlugin
{
    public const string FormatDocx = "DOCX";
    public const string FormatPdf = "PDF";

    private static readonly IReadOnlyList<string> SupportedFields =
    [
        "Number",
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
        "Closed At",
        "Resolution Code",
        "Root Cause Code",
        "Root Cause Date",
        "Workaround"
    ];

    private readonly TicketDataServiceInstance _dataService;

    public TicketDocumentExportPlugin(TicketDataServiceInstance dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public string PluginId => "ticket-document-export";
    public string DisplayName => "Ticket Document Export";
    public string Description => "Exports one ticket or a selected set of tickets as a Word document or PDF using lifecycle-focused fields and optional change history.";

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
            IsRequired = true,
            Placeholder = "Start typing a company name",
            CanonicalFieldName = "Company"
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
            Key = "output_format",
            DisplayName = "Output Format",
            Type = ReportParameterType.Select,
            IsRequired = false,
            Description = "Choose DOCX or PDF.",
            Options = [FormatDocx, FormatPdf]
        },
        new ReportParameterDefinition
        {
            Key = "ticket_keys",
            DisplayName = "Ticket Keys / Numbers",
            Type = ReportParameterType.TextArea,
            IsRequired = false,
            Description = "Optional. Enter one ticket key or number per line, or comma/semicolon separated. Leave blank to export the first matching tickets for the company."
        },
        new ReportParameterDefinition
        {
            Key = "max_tickets",
            DisplayName = "Max Tickets",
            Type = ReportParameterType.Text,
            IsRequired = false,
            Description = "Used when no explicit ticket list is provided. Defaults to 25."
        },
        new ReportParameterDefinition
        {
            Key = "fields",
            DisplayName = "Include Fields",
            Type = ReportParameterType.MultiSelect,
            IsRequired = false,
            Description = "Choose which fields to include in each ticket section.",
            Options = SupportedFields
        },
        new ReportParameterDefinition
        {
            Key = "include_history",
            DisplayName = "Include Field Change History",
            Type = ReportParameterType.Boolean,
            IsRequired = false,
            Description = "Adds the latest recorded field changes for each selected ticket."
        }
    ];

    public async Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        if (!request.Parameters.TryGetValue("itsm_source", out var itsmSource) || string.IsNullOrWhiteSpace(itsmSource))
            return ReportResult.Failure("Required parameter 'itsm_source' is missing.");

        if (!request.Parameters.TryGetValue("company", out var company) || string.IsNullOrWhiteSpace(company))
            return ReportResult.Failure("Required parameter 'company' is missing.");

        if (!request.Parameters.TryGetValue("as_of_date", out var asOfDateRaw) || !DateTime.TryParse(asOfDateRaw, out var asOfDate))
            return ReportResult.Failure("Required parameter 'as_of_date' is missing or invalid.");

        request.Parameters.TryGetValue("output_format", out var format);
        format = string.IsNullOrWhiteSpace(format) ? FormatDocx : format.Trim().ToUpperInvariant();

        request.Parameters.TryGetValue("fields", out var fieldsRaw);
        var selectedFields = ParseMulti(fieldsRaw, SupportedFields);
        if (selectedFields.Count == 0)
        {
            selectedFields = ["Number", "State", "Priority", "Assigned To", "Assignment Group", "Short Description", "Opened At", "Updated On", "Resolved At"];
        }

        request.Parameters.TryGetValue("ticket_keys", out var ticketKeysRaw);
        request.Parameters.TryGetValue("max_tickets", out var maxTicketsRaw);
        var includeHistory = request.Parameters.TryGetValue("include_history", out var includeHistoryRaw)
            && string.Equals(includeHistoryRaw, "true", StringComparison.OrdinalIgnoreCase);

        if (!int.TryParse(maxTicketsRaw, out var maxTickets) || maxTickets <= 0)
            maxTickets = 25;

        try
        {
            var allTickets = (await _dataService.GetTicketsAsync(itsmSource, company, asOfDate)).ToList();
            var requestedTicketIds = ParseTicketIds(ticketKeysRaw);
            List<Ticket> selectedTickets;

            if (requestedTicketIds.Count > 0)
            {
                selectedTickets = allTickets
                    .Where(t => requestedTicketIds.Contains(t.TicketKey)
                        || requestedTicketIds.Contains(TicketFieldHelpers.GetFieldValue(t, "Number", fallback: t.TicketKey)))
                    .OrderBy(t => t.TicketKey, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                selectedTickets = allTickets
                    .OrderBy(t => t.TicketKey, StringComparer.OrdinalIgnoreCase)
                    .Take(maxTickets)
                    .ToList();
            }

            var lifecycle = selectedTickets
                .Select(t => LifecycleAnalytics.BuildLifecycleInfo(t, asOfDate))
                .ToList();

            Dictionary<string, IReadOnlyList<FieldChange>> historyByTicket = new(StringComparer.OrdinalIgnoreCase);
            if (includeHistory)
            {
                foreach (var ticket in selectedTickets)
                {
                    var history = (await _dataService.GetTicketHistoryAsync(company, ticket.TicketKey))
                        .OrderByDescending(h => h.ObservedAt)
                        .ToList();
                    historyByTicket[ticket.TicketKey] = history;
                }
            }

            byte[] content;
            string extension;
            string contentType;
            if (string.Equals(format, FormatPdf, StringComparison.OrdinalIgnoreCase))
            {
                content = TicketDocumentGenerator.GeneratePdf(company, itsmSource, asOfDate, lifecycle, selectedFields, historyByTicket);
                extension = ".pdf";
                contentType = "application/pdf";
            }
            else
            {
                content = TicketDocumentGenerator.GenerateDocx(company, itsmSource, asOfDate, lifecycle, selectedFields, historyByTicket);
                extension = ".docx";
                contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            }

            return new ReportResult
            {
                Success = true,
                OutputType = ReportOutputType.FileDownload,
                FileName = $"UPMS_Ticket_Export_{SanitizeFilePart(company)}_{asOfDate:yyyy-MM-dd}{extension}",
                ContentType = contentType,
                FileContent = content
            };
        }
        catch (Exception ex)
        {
            return ReportResult.Failure($"Failed to generate ticket document: {ex.Message}");
        }
    }

    private static List<string> ParseMulti(string? raw, IReadOnlyList<string> supported)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var supportedSet = new HashSet<string>(supported, StringComparer.OrdinalIgnoreCase);
        return raw
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(supportedSet.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string> ParseTicketIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var parts = raw
            .Split([',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s));

        return new HashSet<string>(parts, StringComparer.OrdinalIgnoreCase);
    }

    private static string SanitizeFilePart(string value)
    {
        StringBuilder sb = new(value.Length);
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
