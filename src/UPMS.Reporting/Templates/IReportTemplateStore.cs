namespace UPMS.Reporting.Templates;

public interface IReportTemplateStore
{
    IReadOnlyList<ReportTemplateMetadata> GetAllTemplates();
    IReadOnlyList<ReportTemplateMetadata> GetTemplatesByKind(ReportTemplateKind kind);
    ReportTemplateMetadata? GetTemplateById(string templateId);
    Task<StoredReportTemplate?> GetTemplateContentAsync(string templateId, CancellationToken ct = default);
    Task<ReportTemplateMetadata> SaveAsync(ReportTemplateUploadRequest request, Stream content, CancellationToken ct = default);
}
