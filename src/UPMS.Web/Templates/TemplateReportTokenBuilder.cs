namespace UPMS.Web.Templates;

using System.Net;
using System.Text;
using UPMS.Data;
using UPMS.Web.Reporting;

public static class TemplateReportTokenBuilder
{
    private static readonly IReadOnlyList<string> DefaultDetailFields =
    [
        "State",
        "Priority",
        "Assigned To",
        "Short Description"
    ];

    public static IReadOnlyDictionary<string, string?> Build(
        string itsmSource,
        string company,
        DateTime asOfDate,
        IReadOnlyList<Ticket> tickets,
        IReadOnlyList<string>? detailFields = null,
        string? requestedBy = null)
    {
        var normalizedFields = (detailFields is { Count: > 0 } ? detailFields : DefaultDetailFields)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var trend = LifecycleAnalytics.BuildMonthlyTrend(tickets, asOfDate);
        var currentPoint = trend.LastOrDefault();
        var firstTicket = tickets.FirstOrDefault();

        Dictionary<string, string?> tokens = new(StringComparer.OrdinalIgnoreCase)
        {
            ["company"] = company,
            ["itsm_source"] = itsmSource,
            ["as_of_date"] = Normalize(asOfDate).ToString("yyyy-MM-dd"),
            ["generated_at_utc"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
            ["requested_by"] = string.IsNullOrWhiteSpace(requestedBy) ? string.Empty : requestedBy,
            ["ticket_count"] = tickets.Count.ToString(),
            ["ticket_keys_csv"] = string.Join(",", tickets.Select(t => t.TicketKey)),
            ["ticket_keys_text"] = string.Join(Environment.NewLine, tickets.Select(t => t.TicketKey)),
            ["tickets_html_table"] = BuildHtmlTable(tickets, normalizedFields),
            ["ticket_rows_html"] = BuildHtmlRows(tickets, normalizedFields),
            ["tickets_text_list"] = BuildTextList(tickets, normalizedFields),
            ["tickets_csv_document"] = BuildCsvDocument(tickets, normalizedFields),
            ["tickets_csv_header"] = string.Join(",", new[] { "ticket_key", "number" }.Concat(normalizedFields).Select(EscapeCsv)),
            ["tickets_csv_rows"] = BuildCsvRows(tickets, normalizedFields),
            ["month_end_chart_svg"] = SvgTrendChartBuilder.BuildLifecycleTrendChart(trend, "Last 12 months lifecycle trend"),
            ["month_end_table_html"] = BuildMonthEndTable(trend),
            ["month_end_backlog_current"] = currentPoint?.BacklogAtMonthEnd.ToString() ?? "0",
            ["month_end_opened_current"] = currentPoint?.OpenedCount.ToString() ?? "0",
            ["month_end_resolved_current"] = currentPoint?.ResolvedCount.ToString() ?? "0",
            ["month_end_updated_current"] = currentPoint?.UpdatedCount.ToString() ?? "0",
            ["month_end_high_priority_backlog_current"] = currentPoint?.HighPriorityBacklogAtMonthEnd.ToString() ?? "0"
        };

        if (firstTicket is not null)
        {
            var lifecycle = LifecycleAnalytics.BuildLifecycleInfo(firstTicket, asOfDate);
            tokens["ticket_key"] = firstTicket.TicketKey;
            tokens["ticket_number"] = TicketFieldHelpers.GetFieldValue(firstTicket, "Number", fallback: firstTicket.TicketKey);
            tokens["ticket_status"] = TicketFieldHelpers.GetFieldValue(firstTicket, "State");
            tokens["ticket_priority"] = TicketFieldHelpers.GetFieldValue(firstTicket, "Priority");
            tokens["ticket_assignee"] = TicketFieldHelpers.GetFieldValue(firstTicket, "Assigned To");
            tokens["ticket_assignment_group"] = TicketFieldHelpers.GetFieldValue(firstTicket, "Assignment Group");
            tokens["ticket_short_description"] = TicketFieldHelpers.GetFieldValue(firstTicket, "Short Description");
            tokens["ticket_opened_at"] = lifecycle.OpenedAt?.ToString("yyyy-MM-dd") ?? string.Empty;
            tokens["ticket_resolved_at"] = lifecycle.ResolvedAt?.ToString("yyyy-MM-dd") ?? string.Empty;
            tokens["ticket_updated_at"] = lifecycle.UpdatedAt?.ToString("yyyy-MM-dd") ?? string.Empty;
            tokens["ticket_age_days"] = lifecycle.AgeDays?.ToString("0.0") ?? string.Empty;
        }

        return tokens;
    }

    private static string BuildHtmlTable(IReadOnlyList<Ticket> tickets, IReadOnlyList<string> detailFields)
    {
        StringBuilder sb = new();
        sb.AppendLine("<table class=\"table table-striped\">");
        sb.AppendLine("<thead><tr><th>Ticket</th><th>Ticket Number</th>");
        foreach (var field in detailFields)
        {
            sb.Append("<th>").Append(WebUtility.HtmlEncode(field)).AppendLine("</th>");
        }
        sb.AppendLine("</tr></thead><tbody>");
        sb.AppendLine(BuildHtmlRows(tickets, detailFields));
        sb.AppendLine("</tbody></table>");
        return sb.ToString();
    }

    private static string BuildHtmlRows(IReadOnlyList<Ticket> tickets, IReadOnlyList<string> detailFields)
    {
        StringBuilder sb = new();
        foreach (var ticket in tickets)
        {
            sb.Append("<tr><td>")
                .Append(WebUtility.HtmlEncode(ticket.TicketKey))
                .Append("</td><td>")
                .Append(WebUtility.HtmlEncode(TicketFieldHelpers.GetFieldValue(ticket, "Number", fallback: ticket.TicketKey)))
                .Append("</td>");

            foreach (var field in detailFields)
            {
                sb.Append("<td>")
                    .Append(WebUtility.HtmlEncode(TicketFieldHelpers.GetFieldValue(ticket, field, fallback: string.Empty)))
                    .Append("</td>");
            }

            sb.AppendLine("</tr>");
        }

        if (tickets.Count == 0)
        {
            sb.AppendLine("<tr><td colspan=\"99\">No tickets selected.</td></tr>");
        }

        return sb.ToString();
    }

    private static string BuildTextList(IReadOnlyList<Ticket> tickets, IReadOnlyList<string> detailFields)
    {
        if (tickets.Count == 0)
            return "No tickets selected.";

        StringBuilder sb = new();
        foreach (var ticket in tickets)
        {
            sb.Append(ticket.TicketKey);
            foreach (var field in detailFields)
            {
                var value = TicketFieldHelpers.GetFieldValue(ticket, field, fallback: string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    sb.Append(" | ")
                        .Append(field)
                        .Append(": ")
                        .Append(value);
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildCsvDocument(IReadOnlyList<Ticket> tickets, IReadOnlyList<string> detailFields)
    {
        var header = string.Join(",", new[] { "ticket_key", "number" }.Concat(detailFields).Select(EscapeCsv));
        var rows = BuildCsvRows(tickets, detailFields);
        return header + Environment.NewLine + rows;
    }

    private static string BuildCsvRows(IReadOnlyList<Ticket> tickets, IReadOnlyList<string> detailFields)
    {
        StringBuilder sb = new();
        foreach (var ticket in tickets)
        {
            var cells = new List<string>
            {
                EscapeCsv(ticket.TicketKey),
                EscapeCsv(TicketFieldHelpers.GetFieldValue(ticket, "Number", fallback: ticket.TicketKey))
            };

            cells.AddRange(detailFields.Select(field => EscapeCsv(TicketFieldHelpers.GetFieldValue(ticket, field, fallback: string.Empty))));
            sb.AppendLine(string.Join(",", cells));
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildMonthEndTable(IReadOnlyList<LifecycleMonthlyPoint> trend)
    {
        StringBuilder sb = new();
        sb.AppendLine("<table class=\"table table-striped\">");
        sb.AppendLine("<thead><tr><th>Month</th><th>Opened</th><th>Resolved</th><th>Updated</th><th>Backlog</th><th>High Priority Backlog</th></tr></thead><tbody>");
        foreach (var point in trend)
        {
            sb.Append("<tr><td>").Append(WebUtility.HtmlEncode(point.Label)).Append("</td>")
                .Append("<td>").Append(point.OpenedCount).Append("</td>")
                .Append("<td>").Append(point.ResolvedCount).Append("</td>")
                .Append("<td>").Append(point.UpdatedCount).Append("</td>")
                .Append("<td>").Append(point.BacklogAtMonthEnd).Append("</td>")
                .Append("<td>").Append(point.HighPriorityBacklogAtMonthEnd).AppendLine("</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }

    private static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
