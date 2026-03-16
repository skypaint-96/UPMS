namespace UPMS.Reporting.Templates;

public sealed class ReportTemplateUploadRequest
{
    public string? TemplateId { get; init; }
    public string? TemplateTypeId { get; init; }
    public required string DisplayName { get; init; }
    public required ReportTemplateKind Kind { get; init; }
    public string? Description { get; init; }
    public string? SubjectTemplate { get; init; }
    public ReportTemplateScope? Scope { get; init; }
    public required string OriginalFileName { get; init; }
    public string UploadedBy { get; init; } = "unknown";
    public bool IsStarterTemplate { get; init; }
}
