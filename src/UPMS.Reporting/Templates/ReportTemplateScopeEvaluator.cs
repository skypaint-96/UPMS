namespace UPMS.Reporting.Templates;

public static class ReportTemplateScopeEvaluator
{
    public static ReportTemplateScope Normalize(ReportTemplateScope? scope)
    {
        var sources = NormalizeValues(scope?.ItsmSources);
        var companies = NormalizeValues(scope?.Companies);
        var pairs = NormalizePairs(scope?.ItsmSourceCompanies);

        return new ReportTemplateScope
        {
            ItsmSources = sources,
            Companies = companies,
            ItsmSourceCompanies = pairs
        };
    }

    public static bool IsApplicable(
        ReportTemplateMetadata template,
        string? itsmSource,
        string? company,
        bool allowPartialContext = false)
    {
        ArgumentNullException.ThrowIfNull(template);
        return IsApplicable(template.Scope, itsmSource, company, allowPartialContext);
    }

    public static bool IsApplicable(
        ReportTemplateScope? scope,
        string? itsmSource,
        string? company,
        bool allowPartialContext = false)
    {
        var normalizedScope = Normalize(scope);
        if (normalizedScope.IsGlobal)
            return true;

        var normalizedSource = NormalizeValue(itsmSource);
        var normalizedCompany = NormalizeValue(company);

        return MatchesValues(normalizedScope.ItsmSources, normalizedSource, allowPartialContext)
            && MatchesValues(normalizedScope.Companies, normalizedCompany, allowPartialContext)
            && MatchesPairs(normalizedScope.ItsmSourceCompanies, normalizedSource, normalizedCompany, allowPartialContext);
    }

    public static string Describe(ReportTemplateScope? scope)
    {
        var normalizedScope = Normalize(scope);
        if (normalizedScope.IsGlobal)
            return "global";

        List<string> parts = [];

        if (normalizedScope.ItsmSources.Length > 0)
            parts.Add($"ITSM sources [{string.Join(", ", normalizedScope.ItsmSources)}]");

        if (normalizedScope.Companies.Length > 0)
            parts.Add($"companies [{string.Join(", ", normalizedScope.Companies)}]");

        if (normalizedScope.ItsmSourceCompanies.Length > 0)
        {
            var pairs = normalizedScope.ItsmSourceCompanies
                .Select(pair => $"{pair.ItsmSource} + {pair.Company}");
            parts.Add($"specific source/company pairs [{string.Join("; ", pairs)}]");
        }

        return string.Join("; ", parts);
    }

    public static string ParseTemplateId(string? selectedOption)
    {
        if (string.IsNullOrWhiteSpace(selectedOption))
            return string.Empty;

        var separatorIndex = selectedOption.IndexOf('|');
        return separatorIndex >= 0
            ? selectedOption[..separatorIndex].Trim()
            : selectedOption.Trim();
    }

    private static bool MatchesValues(string[] allowedValues, string? requestValue, bool allowPartialContext)
    {
        if (allowedValues.Length == 0)
            return true;

        if (requestValue is null)
            return allowPartialContext;

        return allowedValues.Any(value => string.Equals(value, requestValue, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesPairs(
        ReportTemplateScopeCombination[] allowedPairs,
        string? itsmSource,
        string? company,
        bool allowPartialContext)
    {
        if (allowedPairs.Length == 0)
            return true;

        if (!allowPartialContext)
        {
            if (itsmSource is null || company is null)
                return false;

            return allowedPairs.Any(pair =>
                string.Equals(pair.ItsmSource, itsmSource, StringComparison.OrdinalIgnoreCase)
                && string.Equals(pair.Company, company, StringComparison.OrdinalIgnoreCase));
        }

        if (itsmSource is not null && company is not null)
        {
            return allowedPairs.Any(pair =>
                string.Equals(pair.ItsmSource, itsmSource, StringComparison.OrdinalIgnoreCase)
                && string.Equals(pair.Company, company, StringComparison.OrdinalIgnoreCase));
        }

        if (itsmSource is not null)
        {
            return allowedPairs.Any(pair =>
                string.Equals(pair.ItsmSource, itsmSource, StringComparison.OrdinalIgnoreCase));
        }

        if (company is not null)
        {
            return allowedPairs.Any(pair =>
                string.Equals(pair.Company, company, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    private static string[] NormalizeValues(IEnumerable<string>? values)
    {
        HashSet<string> unique = new(StringComparer.OrdinalIgnoreCase);
        List<string> normalized = [];

        foreach (var value in values ?? Array.Empty<string>())
        {
            var trimmed = NormalizeValue(value);
            if (trimmed is null || !unique.Add(trimmed))
                continue;

            normalized.Add(trimmed);
        }

        return normalized.ToArray();
    }

    private static ReportTemplateScopeCombination[] NormalizePairs(IEnumerable<ReportTemplateScopeCombination>? pairs)
    {
        HashSet<string> unique = new(StringComparer.OrdinalIgnoreCase);
        List<ReportTemplateScopeCombination> normalized = [];

        foreach (var pair in pairs ?? Array.Empty<ReportTemplateScopeCombination>())
        {
            if (pair is null)
                continue;

            var itsmSource = NormalizeValue(pair.ItsmSource);
            var company = NormalizeValue(pair.Company);
            if (itsmSource is null || company is null)
                continue;

            var key = $"{itsmSource}\u001F{company}";
            if (!unique.Add(key))
                continue;

            normalized.Add(new ReportTemplateScopeCombination
            {
                ItsmSource = itsmSource,
                Company = company
            });
        }

        return normalized.ToArray();
    }

    private static string? NormalizeValue(string? value)
    {
        // TODO: Replace free-text company matching with stable company identifiers if/when the
        // company domain is normalized. For now we intentionally keep matching pragmatic.
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
