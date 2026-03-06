namespace UPMS.Data;

/// <summary>
/// Builds a stable, globally-unique ticket key.
///
/// Background:
/// - "Ticket number" (INC0012345 / PROJ-123 / etc.) is usually only unique within a single
///   ITSM source instance (and sometimes within a company/tenant).
/// - UPMS uses <see cref="TicketKey"/> as an internal identifier that should be stable
///   across snapshots.
///
/// Format: "{itsmSource}::{companyName}::{ticketNumber}".
///
/// Notes:
/// - We intentionally keep this human-readable.
/// - The key is treated as an opaque string by the app; parsing is optional.
/// </summary>
public static class TicketKeyFactory
{
    private const string Separator = "::";

    public static string Compose(string itsmSource, string companyName, string ticketNumber)
    {
        if (string.IsNullOrWhiteSpace(itsmSource))
            throw new ArgumentException("ITSM source is required.", nameof(itsmSource));
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));
        if (string.IsNullOrWhiteSpace(ticketNumber))
            throw new ArgumentException("Ticket number is required.", nameof(ticketNumber));

        string src = itsmSource.Trim();
        string co = companyName.Trim();
        string num = ticketNumber.Trim();

        // Defensive: prevent accidental separator collisions.
        // (We don't currently need to parse the key, but this keeps it safer.)
        src = src.Replace(Separator, " ", StringComparison.Ordinal);
        co = co.Replace(Separator, " ", StringComparison.Ordinal);
        num = num.Replace(Separator, " ", StringComparison.Ordinal);

        return $"{src}{Separator}{co}{Separator}{num}";
    }

    public static bool TryParse(string ticketKey, out string itsmSource, out string companyName, out string ticketNumber)
    {
        itsmSource = string.Empty;
        companyName = string.Empty;
        ticketNumber = string.Empty;

        if (string.IsNullOrWhiteSpace(ticketKey))
            return false;

        var parts = ticketKey.Split(Separator, StringSplitOptions.None);
        if (parts.Length != 3)
            return false;

        itsmSource = parts[0];
        companyName = parts[1];
        ticketNumber = parts[2];
        return true;
    }
}
