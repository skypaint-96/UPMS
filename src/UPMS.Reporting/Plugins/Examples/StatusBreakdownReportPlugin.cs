namespace UPMS.Reporting.Plugins.Examples;

using System.Net;
using System.Text;
using UPMS.Data;
using UPMS.Reporting;

/// <summary>
/// Example reporting plugin that produces an HTML breakdown of ticket counts by a chosen field
/// (Status by default) as-of a selected date.
/// </summary>
public sealed class StatusBreakdownReportPlugin : IReportPlugin
{
    private readonly TicketDataServiceInstance _dataService;

    public StatusBreakdownReportPlugin(TicketDataServiceInstance dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public string PluginId => "example-status-breakdown";
    public string DisplayName => "Status Breakdown (Example)";
    public string Description => "Counts tickets by Status (or another field) as of a given date. Returns an HTML preview.";

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
            Key = "group_by",
            DisplayName = "Group By",
            Type = ReportParameterType.Select,
            IsRequired = false,
            Description = "Field name to group by. Defaults to 'State' if not set. Legacy aliases such as 'status' are still supported.",
            Options = ["State", "Priority", "Assigned To", "Assignment Group", "Category"]
        },
        new ReportParameterDefinition
        {
            Key = "include_examples",
            DisplayName = "Include Example Tickets",
            Type = ReportParameterType.Boolean,
            IsRequired = false,
            Description = "When enabled, shows up to 25 example tickets under the breakdown."
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

        request.Parameters.TryGetValue("group_by", out var groupBy);
        groupBy = string.IsNullOrWhiteSpace(groupBy) ? "State" : groupBy.Trim();

        bool includeExamples = request.Parameters.TryGetValue("include_examples", out var includeExamplesStr)
            && string.Equals(includeExamplesStr, "true", StringComparison.OrdinalIgnoreCase);

        try
        {
            var tickets = (await _dataService.GetTicketsAsync(itsmSource, company, asOfDate)).ToList();

            var breakdown = tickets
                .Select(t => GetFieldValue(t, groupBy!, fallback: "(blank)"))
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var html = new StringBuilder();
            html.AppendLine("<div class=\"upms-report\">");
            html.AppendLine($"<h2>Status Breakdown</h2>");
            html.AppendLine($"<p><strong>Company:</strong> {WebUtility.HtmlEncode(company)} &nbsp; | &nbsp; <strong>Source:</strong> {WebUtility.HtmlEncode(itsmSource)} &nbsp; | &nbsp; <strong>As of:</strong> {WebUtility.HtmlEncode(asOfDate.ToString("yyyy-MM-dd"))}</p>");
            html.AppendLine($"<p><strong>Total tickets:</strong> {tickets.Count}</p>");

            html.AppendLine("<table class=\"table table-striped\" data-testid=\"report-status-breakdown\">");
            html.AppendLine("<thead><tr><th>" + WebUtility.HtmlEncode(groupBy) + "</th><th>Count</th></tr></thead>");
            html.AppendLine("<tbody>");

            if (breakdown.Count == 0)
            {
                html.AppendLine("<tr><td colspan=\"2\">No tickets found for this company/source at the selected time.</td></tr>");
            }
            else
            {
                foreach (var row in breakdown)
                {
                    html.AppendLine($"<tr><td>{WebUtility.HtmlEncode(row.Value)}</td><td>{row.Count}</td></tr>");
                }
            }

            html.AppendLine("</tbody></table>");

            if (includeExamples && tickets.Count > 0)
            {
                html.AppendLine("<h3>Example Tickets</h3>");
                html.AppendLine("<table class=\"table table-sm\" data-testid=\"report-status-breakdown-examples\">");
                html.AppendLine("<thead><tr><th>Ticket</th><th>" + WebUtility.HtmlEncode(groupBy) + "</th></tr></thead>");
                html.AppendLine("<tbody>");

                foreach (var ticket in tickets.Take(25))
                {
                    var ticketNumber = TicketFieldHelpers.GetFieldValue(ticket, "Number", fallback: ticket.TicketKey);
                    var displayKey = string.IsNullOrWhiteSpace(ticketNumber) ? ticket.TicketKey : ticketNumber;
                    var value = GetFieldValue(ticket, groupBy!, fallback: "(blank)");

                    html.AppendLine($"<tr><td>{WebUtility.HtmlEncode(displayKey)}</td><td>{WebUtility.HtmlEncode(value)}</td></tr>");
                }

                html.AppendLine("</tbody></table>");
            }

            html.AppendLine("</div>");

            return new ReportResult
            {
                Success = true,
                OutputType = ReportOutputType.HtmlContent,
                HtmlContent = html.ToString()
            };
        }
        catch (Exception ex)
        {
            return ReportResult.Failure($"Failed to generate breakdown: {ex.Message}");
        }
    }

    private static string GetFieldValue(Ticket ticket, string fieldName, string fallback) =>
        TicketFieldHelpers.GetFieldValue(ticket, fieldName, fallback);
}
