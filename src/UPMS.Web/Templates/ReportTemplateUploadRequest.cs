namespace UPMS.Web.Templates;

public sealed class ReportTemplateUploadRequest
{
    public required string DisplayName { get; init; }
    public required ReportTemplateKind Kind { get; init; }
    public string? Description { get; init; }
    public string? SubjectTemplate { get; init; }
    public required string OriginalFileName { get; init; }
    public string UploadedBy { get; init; } = "unknown";
}
