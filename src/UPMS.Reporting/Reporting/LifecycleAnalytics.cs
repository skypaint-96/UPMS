namespace UPMS.Reporting;

using UPMS.Data;

public static class LifecycleAnalytics
{
    public static TicketLifecycleInfo BuildLifecycleInfo(Ticket ticket, DateTime asOfDate)
    {
        var openedAt = TicketFieldHelpers.GetOpenedAt(ticket);
        var resolvedAt = TicketFieldHelpers.GetResolvedAt(ticket);
        var updatedAt = TicketFieldHelpers.GetUpdatedAt(ticket);

        double? ageDays = null;
        if (openedAt is not null)
        {
            var end = resolvedAt ?? Normalize(asOfDate);
            ageDays = Math.Max(0, (end - openedAt.Value).TotalDays);
        }

        return new TicketLifecycleInfo
        {
            Ticket = ticket,
            OpenedAt = openedAt,
            ResolvedAt = resolvedAt,
            UpdatedAt = updatedAt,
            Status = TicketFieldHelpers.GetFieldValue(ticket, "State", fallback: "(blank)"),
            Priority = TicketFieldHelpers.GetFieldValue(ticket, "Priority", fallback: "(blank)"),
            AgeDays = ageDays
        };
    }

    public static IReadOnlyList<LifecycleMonthlyPoint> BuildMonthlyTrend(IEnumerable<Ticket> tickets, DateTime asOfDate, int months = 12)
    {
        var normalizedAsOf = Normalize(asOfDate);
        var monthCursor = new DateTime(normalizedAsOf.Year, normalizedAsOf.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-(months - 1));

        var lifecycleTickets = tickets
            .Select(t => BuildLifecycleInfo(t, normalizedAsOf))
            .ToList();

        List<LifecycleMonthlyPoint> result = new(months);
        for (var i = 0; i < months; i++)
        {
            var monthStart = monthCursor.AddMonths(i);
            var naturalMonthEnd = monthStart.AddMonths(1).AddTicks(-1);
            var monthEnd = naturalMonthEnd > normalizedAsOf ? normalizedAsOf : naturalMonthEnd;

            var opened = lifecycleTickets.Count(t => t.OpenedAt is not null && t.OpenedAt.Value >= monthStart && t.OpenedAt.Value <= monthEnd);
            var resolved = lifecycleTickets.Count(t => t.ResolvedAt is not null && t.ResolvedAt.Value >= monthStart && t.ResolvedAt.Value <= monthEnd);
            var updated = lifecycleTickets.Count(t => t.UpdatedAt is not null && t.UpdatedAt.Value >= monthStart && t.UpdatedAt.Value <= monthEnd);
            var backlog = lifecycleTickets.Count(t => t.OpenedAt is not null && t.OpenedAt.Value <= monthEnd && (t.ResolvedAt is null || t.ResolvedAt.Value > monthEnd));
            var highPriorityBacklog = lifecycleTickets.Count(t => t.OpenedAt is not null
                && t.OpenedAt.Value <= monthEnd
                && (t.ResolvedAt is null || t.ResolvedAt.Value > monthEnd)
                && IsHighPriority(t.Priority));

            result.Add(new LifecycleMonthlyPoint
            {
                MonthStart = monthStart,
                MonthEnd = monthEnd,
                Label = monthStart.ToString("MMM yy"),
                OpenedCount = opened,
                ResolvedCount = resolved,
                UpdatedCount = updated,
                BacklogAtMonthEnd = backlog,
                HighPriorityBacklogAtMonthEnd = highPriorityBacklog
            });
        }

        return result;
    }

    public static IReadOnlyList<(string Value, int Count)> BuildBreakdown(IEnumerable<Ticket> tickets, string fieldName)
    {
        return tickets
            .GroupBy(t => TicketFieldHelpers.GetFieldValue(t, fieldName, fallback: "(blank)"), StringComparer.OrdinalIgnoreCase)
            .Select(g => (Value: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsHighPriority(string priority)
    {
        return priority.Equals("high", StringComparison.OrdinalIgnoreCase)
            || priority.Equals("critical", StringComparison.OrdinalIgnoreCase)
            || priority.Equals("sev1", StringComparison.OrdinalIgnoreCase)
            || priority.Equals("sev2", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
