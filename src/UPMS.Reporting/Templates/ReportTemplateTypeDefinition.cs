namespace UPMS.Reporting.Templates;

public enum TemplateRenderingFamily
{
    Text,
    Wordprocessing,
    Spreadsheet,
    Presentation
}

public sealed class ReportTemplateTypeDefinition
{
    public required string TypeId { get; init; }
    public required string DisplayName { get; init; }
    public required ReportTemplateKind Kind { get; init; }
    public required string PrimaryExtension { get; init; }
    public IReadOnlyList<string> Extensions { get; init; } = Array.Empty<string>();
    public required string ContentType { get; init; }
    public required TemplateRenderingFamily RenderingFamily { get; init; }
    public bool SupportsInlineEdit { get; init; }
    public int SortOrder { get; init; }
    public string? Description { get; init; }
    public string? AuthoringGuidance { get; init; }
    public string? StarterTemplateDisplayName { get; init; }
    public string? StarterTemplateDescription { get; init; }
    public string? StarterTemplateResourceName { get; init; }
    public string? DefaultSubjectTemplate { get; init; }

    public bool IsTextLike => RenderingFamily == TemplateRenderingFamily.Text;
    public bool IsOoxmlPackage => RenderingFamily is TemplateRenderingFamily.Wordprocessing or TemplateRenderingFamily.Spreadsheet or TemplateRenderingFamily.Presentation;
}
