namespace UPMS.Reporting.Templates;

public interface IReportTemplateApplicabilityService
{
    IReadOnlyList<ReportTemplateMetadata> GetApplicableTemplates(
        string? itsmSource = null,
        string? company = null,
        bool allowPartialContext = true);

    ReportTemplateSelectionValidation ValidateSelection(
        string? templateIdOrOption,
        string? itsmSource,
        string? company);
}
