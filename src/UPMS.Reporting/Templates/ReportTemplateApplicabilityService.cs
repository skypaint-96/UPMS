namespace UPMS.Reporting.Templates;

public sealed class ReportTemplateApplicabilityService : IReportTemplateApplicabilityService
{
    private readonly IReportTemplateStore _templateStore;

    public ReportTemplateApplicabilityService(IReportTemplateStore templateStore)
    {
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
    }

    public IReadOnlyList<ReportTemplateMetadata> GetApplicableTemplates(
        string? itsmSource = null,
        string? company = null,
        bool allowPartialContext = true)
    {
        return _templateStore.GetAllTemplates()
            .Where(template => ReportTemplateScopeEvaluator.IsApplicable(template, itsmSource, company, allowPartialContext))
            .ToList();
    }

    public ReportTemplateSelectionValidation ValidateSelection(
        string? templateIdOrOption,
        string? itsmSource,
        string? company)
    {
        if (string.IsNullOrWhiteSpace(templateIdOrOption))
            return ReportTemplateSelectionValidation.Failure("Required parameter 'template_id' is missing.");

        if (string.IsNullOrWhiteSpace(itsmSource))
            return ReportTemplateSelectionValidation.Failure("Required parameter 'itsm_source' is missing.");

        if (string.IsNullOrWhiteSpace(company))
            return ReportTemplateSelectionValidation.Failure("Required parameter 'company' is missing.");

        var templateId = ReportTemplateScopeEvaluator.ParseTemplateId(templateIdOrOption);
        if (string.IsNullOrWhiteSpace(templateId))
            return ReportTemplateSelectionValidation.Failure("Required parameter 'template_id' is missing.");

        var template = _templateStore.GetTemplateById(templateId);
        if (template is null)
            return ReportTemplateSelectionValidation.Failure("The selected template could not be loaded.");

        if (!ReportTemplateScopeEvaluator.IsApplicable(template, itsmSource, company, allowPartialContext: false))
        {
            var trimmedSource = itsmSource.Trim();
            var trimmedCompany = company.Trim();
            var scopeSummary = ReportTemplateScopeEvaluator.Describe(template.Scope);
            return ReportTemplateSelectionValidation.Failure(
                $"The selected template '{template.DisplayName}' is not available for ITSM source '{trimmedSource}' and company '{trimmedCompany}'. Allowed scope: {scopeSummary}.");
        }

        return ReportTemplateSelectionValidation.Success(template);
    }
}

public sealed record ReportTemplateSelectionValidation(
    bool IsValid,
    ReportTemplateMetadata? Template,
    string? ErrorMessage)
{
    public static ReportTemplateSelectionValidation Success(ReportTemplateMetadata template)
        => new(true, template, null);

    public static ReportTemplateSelectionValidation Failure(string errorMessage)
        => new(false, null, errorMessage);
}
