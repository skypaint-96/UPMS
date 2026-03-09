namespace UPMS.Reporting;

using System.Globalization;
using UPMS.Data;
using UPMS.Ingestion;

public static class TicketFieldHelpers
{
    private static readonly string[] OpenedCandidates =
    [
        "Opened At",
        "opened_at",
        "opened",
        "opened_date",
        "Created On",
        "created_at",
        "created",
        "created_date"
    ];

    private static readonly string[] ResolvedCandidates =
    [
        "Resolved At",
        "resolved_at",
        "resolved",
        "resolved_date",
        "Closed At",
        "closed_at",
        "closed",
        "closed_date"
    ];

    private static readonly string[] UpdatedCandidates =
    [
        "Updated On",
        "updated_at",
        "updated",
        "last_updated",
        "last_modified",
        "modified_at"
    ];

    public static string GetFieldValue(Ticket ticket, string fieldName, string fallback = "")
    {
        if (TryGetFieldValue(ticket, fieldName, out var value) && !string.IsNullOrWhiteSpace(value))
            return value!;

        return fallback;
    }

    public static bool TryGetFieldValue(Ticket ticket, string fieldName, out string? value)
    {
        foreach (var alias in CanonicalFieldCatalog.GetAliases(fieldName))
        {
            if (ticket.Fields.TryGetValue(alias, out value) && !string.IsNullOrWhiteSpace(value))
                return true;

            foreach (var kv in ticket.Fields)
            {
                if (string.Equals(kv.Key, alias, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(kv.Value))
                {
                    value = kv.Value;
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    public static DateTime? GetOpenedAt(Ticket ticket) =>
        GetFieldDate(ticket, OpenedCandidates) ?? FallbackSnapshotDate(ticket);

    public static DateTime? GetResolvedAt(Ticket ticket) =>
        GetFieldDate(ticket, ResolvedCandidates);

    public static DateTime? GetUpdatedAt(Ticket ticket) =>
        GetFieldDate(ticket, UpdatedCandidates) ?? FallbackObservedAt(ticket);

    public static DateTime? GetFieldDate(Ticket ticket, params string[] fieldCandidates)
    {
        foreach (var candidate in fieldCandidates)
        {
            if (!TryGetFieldValue(ticket, candidate, out var raw) || string.IsNullOrWhiteSpace(raw))
                continue;

            if (TryParseDate(raw!, out var parsed))
                return parsed;
        }

        return null;
    }

    public static bool IsHighPriority(Ticket ticket)
    {
        var priority = GetFieldValue(ticket, "Priority", fallback: string.Empty);
        return priority.Equals("high", StringComparison.OrdinalIgnoreCase)
            || priority.Equals("critical", StringComparison.OrdinalIgnoreCase)
            || priority.Equals("sev1", StringComparison.OrdinalIgnoreCase)
            || priority.Equals("sev2", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsOpenAt(Ticket ticket, DateTime at)
    {
        var openedAt = GetOpenedAt(ticket);
        if (openedAt is null)
            return false;

        var resolvedAt = GetResolvedAt(ticket);
        return openedAt.Value <= at && (resolvedAt is null || resolvedAt.Value > at);
    }

    public static bool TryParseDate(string value, out DateTime parsed)
    {
        value = value.Trim();

        if (DateTime.TryParseExact(value,
                [
                    "yyyy-MM-dd HH:mm:ss",
                    "yyyy-MM-ddTHH:mm:ss",
                    "yyyy-MM-ddTHH:mm:ssZ",
                    "yyyy-MM-ddTHH:mm:ss.fffZ",
                    "yyyy-MM-dd",
                    "MM/dd/yyyy",
                    "dd/MM/yyyy",
                    "o",
                    "s"
                ],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsed))
        {
            parsed = Normalize(parsed);
            return true;
        }

        if (DateTime.TryParse(value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsed))
        {
            parsed = Normalize(parsed);
            return true;
        }

        return false;
    }

    private static DateTime? FallbackSnapshotDate(Ticket ticket) =>
        ticket.SnapshotDate == default ? null : Normalize(ticket.SnapshotDate);

    private static DateTime? FallbackObservedAt(Ticket ticket) =>
        ticket.ObservedAt == default ? null : Normalize(ticket.ObservedAt);

    private static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
