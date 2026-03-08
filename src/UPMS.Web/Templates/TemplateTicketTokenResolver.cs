namespace UPMS.Web.Templates;

using System.Text;
using System.Text.RegularExpressions;
using UPMS.Data;
using UPMS.Web.Reporting;

internal static class TemplateTicketTokenResolver
{
    private static readonly Regex CamelBoundaryRegex = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);

    public static bool TryResolve(Ticket ticket, int ticketIndex, string tokenSuffix, out string? value)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenSuffix);

        string suffix = tokenSuffix.Trim();

        switch (suffix.ToLowerInvariant())
        {
            case "ticketkey":
            case "key":
                value = ticket.TicketKey;
                return true;
            case "company":
            case "companyname":
                value = ticket.CompanyName;
                return true;
            case "itsmsource":
            case "source":
                value = ticket.ItsmSource;
                return true;
            case "snapshotid":
                value = ticket.SnapshotId.ToString();
                return true;
            case "snapshotdate":
                value = Normalize(ticket.SnapshotDate).ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
                return true;
            case "observedat":
                value = Normalize(ticket.ObservedAt).ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
                return true;
            case "index":
            case "position":
                value = (ticketIndex + 1).ToString();
                return true;
        }

        if (TicketFieldHelpers.TryGetFieldValue(ticket, suffix, out value))
            return true;

        var normalizedCandidate = NormalizeFieldLikeToken(suffix);
        if (!string.Equals(normalizedCandidate, suffix, StringComparison.OrdinalIgnoreCase)
            && TicketFieldHelpers.TryGetFieldValue(ticket, normalizedCandidate, out value))
        {
            return true;
        }

        string tokenKey = NormalizeComparableKey(suffix);
        string normalizedTokenKey = NormalizeComparableKey(normalizedCandidate);
        foreach (var field in ticket.Fields)
        {
            if (string.Equals(NormalizeComparableKey(field.Key), tokenKey, StringComparison.Ordinal)
                || string.Equals(NormalizeComparableKey(field.Key), normalizedTokenKey, StringComparison.Ordinal))
            {
                value = field.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    public static string NormalizeFieldLikeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        string normalized = token.Trim();
        normalized = normalized.Replace('.', ' ');
        normalized = normalized.Replace('_', ' ');
        normalized = normalized.Replace('-', ' ');
        normalized = CamelBoundaryRegex.Replace(normalized, "$1 $2");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        return normalized.Trim();
    }

    public static string NormalizeComparableKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder sb = new(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString();
    }

    private static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
