namespace UPMS.Web.Templates;

public sealed class ReportTemplateMetadata
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required ReportTemplateKind Kind { get; init; }
    public string? Description { get; init; }
    public string? SubjectTemplate { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public required string ContentType { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }
    public string UploadedBy { get; init; } = "unknown";
}
