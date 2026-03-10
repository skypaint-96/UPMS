namespace UPMS.Reporting.Plugins.Examples;

using System.Net;
using System.Text;
using UPMS.Data;
using UPMS.Reporting;

/// <summary>
/// Example report plugin that focuses on ticket lifecycle dates (opened/resolved/updated)
/// rather than the ticket's latest observed snapshot timestamp.
/// </summary>
public sealed class MonthEndLifecycleReportPlugin : IReportPlugin
{
    private static readonly IReadOnlyList<string> SupportedBreakdownFields =
    [
        "State",
        "Priority",
        "Assignment Group",
        "Assigned To",
        "Category",
        "Business Service",
        "Service Offering"
    ];

    private static readonly IReadOnlyList<string> SupportedDetailFields =
    [
        "Number",
        "State",
        "Priority",
        "Assignment Group",
        "Assigned To",
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
    ];

    private readonly TicketDataServiceInstance _dataService;

    public MonthEndLifecycleReportPlugin(TicketDataServiceInstance dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public string PluginId => "example-month-end-lifecycle";
    public string DisplayName => "Month End Lifecycle Report (Example)";
    public string Description => "Builds a 12 month trend using recorded opened/resolved/updated ticket dates, with configurable breakdown and detail fields.";

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
            DisplayName = "Month End / As Of Date",
            Type = ReportParameterType.Date,
            IsRequired = true,
            Description = "The report reconstructs current tickets as of this date, then calculates lifecycle metrics from recorded ticket date fields."
        },
        new ReportParameterDefinition
        {
            Key = "breakdown_field",
            DisplayName = "Current Breakdown Field",
            Type = ReportParameterType.Select,
            IsRequired = false,
            Description = "Choose which current field to group the latest ticket set by.",
            Options = SupportedBreakdownFields
        },
        new ReportParameterDefinition
        {
            Key = "detail_fields",
            DisplayName = "Ticket Detail Fields",
            Type = ReportParameterType.MultiSelect,
            IsRequired = false,
            Description = "Choose which fields to show in the ticket detail section.",
            Options = SupportedDetailFields
        },
        new ReportParameterDefinition
        {
            Key = "include_ticket_table",
            DisplayName = "Include Ticket Table",
            Type = ReportParameterType.Boolean,
            IsRequired = false,
            Description = "Show the current ticket set with the selected detail fields."
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

        request.Parameters.TryGetValue("breakdown_field", out var breakdownField);
        breakdownField = string.IsNullOrWhiteSpace(breakdownField) ? "State" : breakdownField.Trim();

        request.Parameters.TryGetValue("detail_fields", out var detailFieldsRaw);
        var detailFields = ParseMulti(detailFieldsRaw, SupportedDetailFields);
        if (detailFields.Count == 0)
        {
            detailFields = ["State", "Priority", "Assigned To", "Short Description"];
        }

        var includeTicketTable = request.Parameters.TryGetValue("include_ticket_table", out var includeTicketTableRaw)
            && string.Equals(includeTicketTableRaw, "true", StringComparison.OrdinalIgnoreCase);

        try
        {
            var tickets = (await _dataService.GetTicketsAsync(itsmSource, company, asOfDate)).ToList();
            var trend = LifecycleAnalytics.BuildMonthlyTrend(tickets, asOfDate);
            var currentPoint = trend.LastOrDefault();
            var breakdown = LifecycleAnalytics.BuildBreakdown(tickets, breakdownField);

            StringBuilder html = new();
            html.AppendLine("<div class=\"upms-report\">");
            html.AppendLine("<style>.upms-report{font-family:Arial,sans-serif;color:#222}.upms-kpis{display:flex;gap:12px;flex-wrap:wrap;margin:16px 0}.upms-kpi{border:1px solid #ddd;border-radius:6px;padding:12px 16px;min-width:150px;background:#fafafa}.upms-kpi-value{font-size:1.8rem;font-weight:600}.upms-note{background:#f8fafc;border-left:4px solid #2563eb;padding:12px;margin:12px 0}.upms-chart{margin:16px 0;padding:12px;border:1px solid #ddd;border-radius:6px;background:white}.upms-table{width:100%;border-collapse:collapse}.upms-table th,.upms-table td{border-bottom:1px solid #ddd;padding:8px;text-align:left}.upms-table th{background:#f5f5f5}</style>");
            html.AppendLine($"<h2>Month End Lifecycle Report</h2><p><strong>Company:</strong> {Encode(company)} | <strong>Source:</strong> {Encode(itsmSource)} | <strong>As of:</strong> {asOfDate:yyyy-MM-dd}</p>");
            html.AppendLine("<div class=\"upms-note\">This report uses recorded lifecycle fields such as <code>Opened At</code>, <code>Created On</code>, <code>Resolved At</code>, <code>Closed At</code>, and <code>Updated On</code> where available. Snapshot timestamps are only used to reconstruct the ticket set and as a fallback when lifecycle dates are missing. Legacy aliases like <code>status</code> and <code>opened_at</code> are still recognised.</div>");

            html.AppendLine("<div class=\"upms-kpis\">");
            html.AppendLine(BuildKpi("Current Backlog", currentPoint?.BacklogAtMonthEnd ?? 0));
            html.AppendLine(BuildKpi("Opened This Month", currentPoint?.OpenedCount ?? 0));
            html.AppendLine(BuildKpi("Resolved This Month", currentPoint?.ResolvedCount ?? 0));
            html.AppendLine(BuildKpi("High Priority Backlog", currentPoint?.HighPriorityBacklogAtMonthEnd ?? 0));
            html.AppendLine("</div>");

            html.AppendLine("<section class=\"upms-chart\">");
            html.AppendLine("<h3>Last 12 Months</h3>");
            html.AppendLine(SvgTrendChartBuilder.BuildLifecycleTrendChart(trend, "Backlog vs opened vs resolved"));
            html.AppendLine("</section>");

            html.AppendLine("<section>");
            html.AppendLine("<h3>Month End Totals</h3>");
            html.AppendLine("<table class=\"upms-table\" data-testid=\"report-month-end-table\"><thead><tr><th>Month</th><th>Opened</th><th>Resolved</th><th>Updated</th><th>Backlog</th><th>High Priority Backlog</th></tr></thead><tbody>");
            foreach (var point in trend)
            {
                html.AppendLine($"<tr><td>{Encode(point.Label)}</td><td>{point.OpenedCount}</td><td>{point.ResolvedCount}</td><td>{point.UpdatedCount}</td><td>{point.BacklogAtMonthEnd}</td><td>{point.HighPriorityBacklogAtMonthEnd}</td></tr>");
            }
            html.AppendLine("</tbody></table>");
            html.AppendLine("</section>");

            html.AppendLine("<section>");
            html.AppendLine($"<h3>Current Breakdown by {Encode(breakdownField)}</h3>");
            html.AppendLine("<table class=\"upms-table\" data-testid=\"report-month-end-breakdown\"><thead><tr><th>Value</th><th>Count</th></tr></thead><tbody>");
            foreach (var row in breakdown)
            {
                html.AppendLine($"<tr><td>{Encode(row.Value)}</td><td>{row.Count}</td></tr>");
            }
            if (breakdown.Count == 0)
            {
                html.AppendLine("<tr><td colspan=\"2\">No tickets available for the selected scope.</td></tr>");
            }
            html.AppendLine("</tbody></table>");
            html.AppendLine("</section>");

            if (includeTicketTable)
            {
                html.AppendLine("<section>");
                html.AppendLine("<h3>Current Tickets</h3>");
                html.AppendLine("<table class=\"upms-table\" data-testid=\"report-month-end-tickets\"><thead><tr><th>Ticket</th><th>Ticket Number</th>");
                foreach (var field in detailFields)
                {
                    html.AppendLine($"<th>{Encode(field)}</th>");
                }
                html.AppendLine("</tr></thead><tbody>");

                foreach (var ticket in tickets)
                {
                    html.AppendLine("<tr>");
                    html.AppendLine($"<td>{Encode(ticket.TicketKey)}</td>");
                    html.AppendLine($"<td>{Encode(TicketFieldHelpers.GetFieldValue(ticket, "Number", fallback: ticket.TicketKey))}</td>");
                    foreach (var field in detailFields)
                    {
                        html.AppendLine($"<td>{Encode(TicketFieldHelpers.GetFieldValue(ticket, field, fallback: string.Empty))}</td>");
                    }
                    html.AppendLine("</tr>");
                }

                if (tickets.Count == 0)
                {
                    html.AppendLine($"<tr><td colspan=\"{detailFields.Count + 2}\">No tickets available.</td></tr>");
                }

                html.AppendLine("</tbody></table>");
                html.AppendLine("</section>");
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
            return ReportResult.Failure($"Failed to generate month end lifecycle report: {ex.Message}");
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

    private static string BuildKpi(string label, int value) =>
        $"<div class=\"upms-kpi\"><div>{Encode(label)}</div><div class=\"upms-kpi-value\">{value}</div></div>";

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
