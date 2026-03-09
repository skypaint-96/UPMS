namespace UPMS.Reporting.Templates;

public interface IReportTemplateTypeProvider
{
    IReadOnlyList<ReportTemplateTypeDefinition> GetTemplateTypes();
}
