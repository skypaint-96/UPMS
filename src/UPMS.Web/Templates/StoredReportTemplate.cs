namespace UPMS.Web.Templates;

public sealed class StoredReportTemplate
{
    public required ReportTemplateMetadata Metadata { get; init; }
    public required byte[] FileContent { get; init; }
}
