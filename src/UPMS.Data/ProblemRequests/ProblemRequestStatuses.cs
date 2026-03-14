namespace UPMS.Data.ProblemRequests;

public static class ProblemRequestStatuses
{
    public const string New = "New";
    public const string UnderReview = "Under Review";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string ConvertedLinked = "Converted / Linked";

    public static IReadOnlyList<string> All { get; } =
    [
        New,
        UnderReview,
        Accepted,
        Rejected,
        ConvertedLinked
    ];

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [New] = New,
        [UnderReview] = UnderReview,
        ["UnderReview"] = UnderReview,
        [Accepted] = Accepted,
        [Rejected] = Rejected,
        [ConvertedLinked] = ConvertedLinked,
        ["Converted/Linked"] = ConvertedLinked,
        ["Linked"] = ConvertedLinked
    };

    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ProblemRequestValidationException("status", "Status is required.");

        var trimmed = status.Trim();
        if (Aliases.TryGetValue(trimmed, out var normalized))
            return normalized;

        throw new ProblemRequestValidationException("status", $"Status must be one of: {string.Join(", ", All)}.");
    }
}
