namespace UPMS.Reporting.Templates;

public sealed class ReportTemplateTypeRegistry
{
    private readonly IReadOnlyList<ReportTemplateTypeDefinition> _definitions;
    private readonly IReadOnlyDictionary<string, ReportTemplateTypeDefinition> _byTypeId;
    private readonly ILookup<string, ReportTemplateTypeDefinition> _byExtension;

    public ReportTemplateTypeRegistry(IEnumerable<IReportTemplateTypeProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var definitions = providers
            .SelectMany(provider => provider.GetTemplateTypes() ?? Array.Empty<ReportTemplateTypeDefinition>())
            .Select(NormalizeDefinition)
            .OrderBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var duplicateTypeIds = definitions
            .GroupBy(definition => definition.TypeId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicateTypeIds.Count > 0)
            throw new InvalidOperationException($"Duplicate report template type IDs detected: {string.Join(", ", duplicateTypeIds)}");

        _definitions = definitions.AsReadOnly();
        _byTypeId = _definitions.ToDictionary(definition => definition.TypeId, StringComparer.OrdinalIgnoreCase);
        _byExtension = _definitions
            .SelectMany(definition => definition.Extensions.Select(extension => new KeyValuePair<string, ReportTemplateTypeDefinition>(NormalizeExtension(extension), definition)))
            .ToLookup(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ReportTemplateTypeDefinition> GetAll() => _definitions;

    public ReportTemplateTypeDefinition? GetByTypeId(string? typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return null;

        return _byTypeId.TryGetValue(typeId.Trim(), out var definition)
            ? definition
            : null;
    }

    public IReadOnlyList<ReportTemplateTypeDefinition> GetByKind(ReportTemplateKind kind) =>
        _definitions.Where(definition => definition.Kind == kind).ToList();

    public IReadOnlyList<ReportTemplateTypeDefinition> GetByExtension(string extension)
    {
        var normalized = NormalizeExtension(extension);
        return _byExtension[normalized].ToList();
    }

    public ReportTemplateTypeDefinition? Resolve(string? typeId, string extension, ReportTemplateKind? preferredKind = null)
    {
        var normalizedExtension = NormalizeExtension(extension);

        if (!string.IsNullOrWhiteSpace(typeId))
        {
            var definition = GetByTypeId(typeId);
            if (definition is null)
                return null;

            return definition.Extensions.Contains(normalizedExtension, StringComparer.OrdinalIgnoreCase)
                ? definition
                : null;
        }

        var candidates = GetByExtension(normalizedExtension);
        if (preferredKind is not null)
        {
            candidates = candidates
                .Where(candidate => candidate.Kind == preferredKind.Value)
                .ToList();
        }

        return candidates.Count == 0 ? null : candidates[0];
    }

    public ReportTemplateTypeDefinition? Resolve(ReportTemplateMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return Resolve(metadata.TemplateTypeId, metadata.Extension, metadata.Kind);
    }

    public static string NormalizeExtension(string? extension) => ReportTemplateContentTypeMapper.Normalize(extension);

    private static ReportTemplateTypeDefinition NormalizeDefinition(ReportTemplateTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var primaryExtension = NormalizeExtension(definition.PrimaryExtension);
        var extensions = definition.Extensions.Count == 0
            ? [primaryExtension]
            : definition.Extensions
                .Select(NormalizeExtension)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (!extensions.Contains(primaryExtension, StringComparer.OrdinalIgnoreCase))
        {
            extensions = [primaryExtension, .. extensions];
        }

        return new ReportTemplateTypeDefinition
        {
            TypeId = definition.TypeId.Trim(),
            DisplayName = definition.DisplayName.Trim(),
            Kind = definition.Kind,
            PrimaryExtension = primaryExtension,
            Extensions = extensions,
            ContentType = definition.ContentType.Trim(),
            RenderingFamily = definition.RenderingFamily,
            SupportsInlineEdit = definition.SupportsInlineEdit,
            SortOrder = definition.SortOrder,
            Description = definition.Description?.Trim(),
            AuthoringGuidance = definition.AuthoringGuidance?.Trim(),
            StarterTemplateDisplayName = definition.StarterTemplateDisplayName?.Trim(),
            StarterTemplateDescription = definition.StarterTemplateDescription?.Trim(),
            StarterTemplateResourceName = definition.StarterTemplateResourceName?.Trim(),
            DefaultSubjectTemplate = definition.DefaultSubjectTemplate?.Trim()
        };
    }
}
