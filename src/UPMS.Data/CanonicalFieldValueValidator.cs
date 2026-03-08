namespace UPMS.Data;

using System.Globalization;

public static class CanonicalFieldValueValidator
{
    public static bool IsValid(CanonicalFieldDataType dataType, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        string trimmed = value.Trim();

        return dataType switch
        {
            CanonicalFieldDataType.Text => true,
            CanonicalFieldDataType.Integer => long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            CanonicalFieldDataType.Decimal => decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            CanonicalFieldDataType.DateTime => TryParseDateTime(trimmed, out _),
            CanonicalFieldDataType.Boolean => TryParseBoolean(trimmed, out _),
            _ => true
        };
    }

    public static bool TryParseDateTime(string value, out DateTime parsed)
    {
        if (DateTime.TryParseExact(
                value,
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

        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out parsed))
        {
            parsed = Normalize(parsed);
            return true;
        }

        return false;
    }

    public static bool TryParseBoolean(string value, out bool parsed)
    {
        if (bool.TryParse(value, out parsed))
            return true;

        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "y":
            case "yes":
            case "true":
                parsed = true;
                return true;
            case "0":
            case "n":
            case "no":
            case "false":
                parsed = false;
                return true;
            default:
                parsed = false;
                return false;
        }
    }

    private static DateTime Normalize(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
