namespace UPMS.Web.Services;

public interface ICanonicalValueSuggestionService
{
    Task<IReadOnlyList<string>> GetSuggestionsAsync(
        string canonicalFieldName,
        IEnumerable<string>? itsmSources = null,
        int max = 12,
        CancellationToken ct = default);
}
