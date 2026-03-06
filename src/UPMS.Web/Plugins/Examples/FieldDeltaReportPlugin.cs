namespace UPMS.Web.Plugins.Examples;

using System.Net;
using System.Text;
using UPMS.Data;

/// <summary>
/// Example reporting plugin that compares ticket state between two dates and reports changes.
/// Useful as a starting point for month-over-month reporting.
/// </summary>
public sealed class FieldDeltaReportPlugin : IReportPlugin
{
    private readonly TicketDataServiceInstance _dataService;

    public FieldDeltaReportPlugin(TicketDataServiceInstance dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public string PluginId => "example-field-delta";
    public string DisplayName => "Field Delta (Example)";
    public string Description => "Compares ticket state between two dates and shows changes (new/removed/changed).";

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
            Key = "date_range",
            DisplayName = "Date Range",
            Type = ReportParameterType.DateRange,
            IsRequired = true,
            Description = "Compare ticket state between the From and To dates."
        },
        new ReportParameterDefinition
        {
            Key = "field_name",
            DisplayName = "Field",
            Type = ReportParameterType.Select,
            IsRequired = false,
            Description = "Which field to compare between dates (defaults to 'status').",
            Options = ["status", "priority", "assignee", "assignment_group"]
        },
        new ReportParameterDefinition
        {
            Key = "include_ticket_list",
            DisplayName = "Include Changed Tickets",
            Type = ReportParameterType.Boolean,
            IsRequired = false,
            Description = "When enabled, lists individual tickets that changed within the range."
        }
    ];

    public async Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        if (!request.Parameters.TryGetValue("itsm_source", out var itsmSource) || string.IsNullOrWhiteSpace(itsmSource))
            return ReportResult.Failure("Required parameter 'itsm_source' is missing.");

        if (!request.Parameters.TryGetValue("company", out var company) || string.IsNullOrWhiteSpace(company))
            return ReportResult.Failure("Required parameter 'company' is missing.");

        var fromKey = "date_range_from";
        var toKey = "date_range_to";

        if (!request.Parameters.TryGetValue(fromKey, out var fromStr) || !DateTime.TryParse(fromStr, out var fromDate))
            return ReportResult.Failure("Required parameter 'date_range_from' is missing or invalid.");

        if (!request.Parameters.TryGetValue(toKey, out var toStr) || !DateTime.TryParse(toStr, out var toDate))
            return ReportResult.Failure("Required parameter 'date_range_to' is missing or invalid.");

        request.Parameters.TryGetValue("field_name", out var fieldName);
        fieldName = string.IsNullOrWhiteSpace(fieldName) ? "status" : fieldName.Trim();

        bool includeTicketList = request.Parameters.TryGetValue("include_ticket_list", out var includeStr)
            && string.Equals(includeStr, "true", StringComparison.OrdinalIgnoreCase);

        try
        {
            var fromTickets = (await _dataService.GetTicketsAsync(itsmSource, company, fromDate)).ToList();
            var toTickets = (await _dataService.GetTicketsAsync(itsmSource, company, toDate)).ToList();

            var fromMap = fromTickets.ToDictionary(t => t.TicketKey, StringComparer.Ordinal);
            var toMap = toTickets.ToDictionary(t => t.TicketKey, StringComparer.Ordinal);

            var fromKeys = fromMap.Keys.ToHashSet(StringComparer.Ordinal);
            var toKeys = toMap.Keys.ToHashSet(StringComparer.Ordinal);

            var newKeys = toKeys.Except(fromKeys).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var removedKeys = fromKeys.Except(toKeys).OrderBy(k => k, StringComparer.Ordinal).ToList();
            var commonKeys = fromKeys.Intersect(toKeys).OrderBy(k => k, StringComparer.Ordinal).ToList();

            var changes = new List<TicketChange>();
            foreach (var key in commonKeys)
            {
                var a = GetFieldValue(fromMap[key], fieldName!, fallback: "(blank)");
                var b = GetFieldValue(toMap[key], fieldName!, fallback: "(blank)");

                if (!string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                    changes.Add(new TicketChange(key, a, b));
            }

            var transitions = changes
                .GroupBy(c => (From: c.FromValue, To: c.ToValue), new TransitionComparer())
                .Select(g => new { g.Key.From, g.Key.To, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.From, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.To, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var html = new StringBuilder();
            html.AppendLine("<div class=\"upms-report\">\n");
            html.AppendLine("<h2>Field Delta</h2>");
            html.AppendLine($"<p><strong>Company:</strong> {WebUtility.HtmlEncode(company)} &nbsp; | &nbsp; <strong>Source:</strong> {WebUtility.HtmlEncode(itsmSource)} &nbsp; | &nbsp; <strong>Field:</strong> {WebUtility.HtmlEncode(fieldName)}<br/>" +
                           $"<strong>From:</strong> {WebUtility.HtmlEncode(fromDate.ToString("yyyy-MM-dd"))} &nbsp; <strong>To:</strong> {WebUtility.HtmlEncode(toDate.ToString("yyyy-MM-dd"))}</p>");

            html.AppendLine("<ul>");
            html.AppendLine($"<li><strong>Tickets at From:</strong> {fromTickets.Count}</li>");
            html.AppendLine($"<li><strong>Tickets at To:</strong> {toTickets.Count}</li>");
            html.AppendLine($"<li><strong>New tickets:</strong> {newKeys.Count}</li>");
            html.AppendLine($"<li><strong>Removed tickets:</strong> {removedKeys.Count}</li>");
            html.AppendLine($"<li><strong>Changed tickets:</strong> {changes.Count}</li>");
            html.AppendLine("</ul>");

            html.AppendLine("<h3>Transitions</h3>");
            html.AppendLine("<table class=\"table table-striped\" data-testid=\"report-field-delta-transitions\">\n<thead><tr><th>From</th><th>To</th><th>Count</th></tr></thead><tbody>");

            if (transitions.Count == 0)
            {
                html.AppendLine("<tr><td colspan=\"3\">No changes detected for the selected field.</td></tr>");
            }
            else
            {
                foreach (var t in transitions)
                {
                    html.AppendLine($"<tr><td>{WebUtility.HtmlEncode(t.From)}</td><td>{WebUtility.HtmlEncode(t.To)}</td><td>{t.Count}</td></tr>");
                }
            }

            html.AppendLine("</tbody></table>");

            if (includeTicketList && changes.Count > 0)
            {
                html.AppendLine("<h3>Changed Tickets</h3>");
                html.AppendLine("<table class=\"table table-sm\" data-testid=\"report-field-delta-changes\">\n<thead><tr><th>Ticket</th><th>From</th><th>To</th></tr></thead><tbody>");

                foreach (var c in changes.Take(200))
                {
                    // Link to the To-date ticket view.
                    var url = $"/tickets/{Uri.EscapeDataString(company)}/{Uri.EscapeDataString(itsmSource)}/{Uri.EscapeDataString(c.TicketKey)}?asOf={Uri.EscapeDataString(toDate.ToString("yyyy-MM-ddTHH:mm"))}";
                    html.AppendLine($"<tr><td><a href=\"{url}\">{WebUtility.HtmlEncode(c.TicketKey)}</a></td><td>{WebUtility.HtmlEncode(c.FromValue)}</td><td>{WebUtility.HtmlEncode(c.ToValue)}</td></tr>");
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
            return ReportResult.Failure($"Failed to generate delta report: {ex.Message}");
        }
    }

    private static string GetFieldValue(Ticket ticket, string fieldName, string fallback)
    {
        if (ticket.Fields.TryGetValue(fieldName, out var v) && !string.IsNullOrWhiteSpace(v))
            return v!;

        foreach (var kv in ticket.Fields)
        {
            if (string.Equals(kv.Key, fieldName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(kv.Value))
            {
                return kv.Value!;
            }
        }

        return fallback;
    }

    private readonly record struct TicketChange(string TicketKey, string FromValue, string ToValue);

    private sealed class TransitionComparer : IEqualityComparer<(string From, string To)>
    {
        public bool Equals((string From, string To) x, (string From, string To) y)
        {
            return string.Equals(x.From, y.From, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.To, y.To, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string From, string To) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.From ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.To ?? string.Empty));
        }
    }
}
