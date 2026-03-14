namespace UPMS.Reporting.Templates;

public sealed class ReportTemplateMetadata
{
    public required string Id { get; init; }
    public string? TemplateTypeId { get; init; }
    public required string DisplayName { get; init; }
    public required ReportTemplateKind Kind { get; init; }
    public string? Description { get; init; }
    public string? SubjectTemplate { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public required string ContentType { get; init; }
    public ReportTemplateScope Scope { get; init; } = new();
    public required DateTimeOffset UploadedAt { get; init; }
    public string UploadedBy { get; init; } = "unknown";
    public DateTimeOffset? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
    public bool IsStarterTemplate { get; init; }
}
