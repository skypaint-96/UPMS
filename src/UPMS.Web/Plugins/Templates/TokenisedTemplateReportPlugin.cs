namespace UPMS.Web.Plugins.Templates;

using System.Text;
using UPMS.Data;
using UPMS.Web.Templates;
using UPMS.Web.Reporting;
using UPMS.Web.Plugins.Email;

/// <summary>
/// Generic template-driven report plugin. Supports uploaded HTML/email/document/spreadsheet templates
/// with {{token}} placeholders that are filled from the selected ticket scope.
/// </summary>
public sealed class TokenisedTemplateReportPlugin : IReportPlugin
{
    public const string OutputModeAuto = "Auto";
    public const string OutputModePreviewHtml = "Preview HTML";
    public const string OutputModeDownloadFile = "Download Filled File";
    public const string OutputModeDownloadEml = "Download EML Draft";

    private static readonly IReadOnlyList<string> SupportedDetailFields =
    [
        "Number",
        "State",
        "Priority",
        "Assigned To",
        "Assignment Group",
        "Short Description",
        "Description",
        "Category",
        "Subcategory",
        "Business Service",
        "Service Offering",
        "Opened At",
        "Created On",
        "Updated On",
        "Resolved At",
        "Closed At",
        "Resolution Code",
        "Root Cause Code",
        "Root Cause Date",
        "Workaround"
    ];

    private readonly TicketDataServiceInstance _dataService;
    private readonly IReportTemplateStore _templateStore;

    public TokenisedTemplateReportPlugin(TicketDataServiceInstance dataService, IReportTemplateStore templateStore)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
    }

    public string PluginId => "tokenised-template-report";
    public string DisplayName => "Tokenised Template Fill";
    public string Description => "Fills uploaded email, document, or spreadsheet templates using {{token}} fields and returns the rendered output.";

    public IReadOnlyList<ReportParameterDefinition> Parameters
    {
        get
        {
            var templateOptions = _templateStore.GetAllTemplates()
                .Select(t => $"{t.Id} | {t.DisplayName} ({t.Extension})")
                .ToList();

            return
            [
                new ReportParameterDefinition
                {
                    Key = "template_id",
                    DisplayName = "Template",
                    Type = ReportParameterType.Select,
                    IsRequired = true,
                    Description = "Choose an uploaded template from the report template library.",
                    Options = templateOptions
                },
                new ReportParameterDefinition
                {
                    Key = "output_mode",
                    DisplayName = "Output Mode",
                    Type = ReportParameterType.Select,
                    IsRequired = false,
                    Description = "Auto chooses the most natural output based on template type.",
                    Options = [OutputModeAuto, OutputModePreviewHtml, OutputModeDownloadFile, OutputModeDownloadEml]
                },
                new ReportParameterDefinition
                {
                    Key = "itsm_source",
                    DisplayName = "ITSM Source",
                    Type = ReportParameterType.ItsmSource,
                    IsRequired = true
                },
                new ReportParameterDefinition
                {
                    Key = "company",
                    DisplayName = "Company",
                    Type = ReportParameterType.Text,
                    IsRequired = true
                },
                new ReportParameterDefinition
                {
                    Key = "as_of_date",
                    DisplayName = "As Of Date",
                    Type = ReportParameterType.Date,
                    IsRequired = true
                },
                new ReportParameterDefinition
                {
                    Key = "ticket_keys",
                    DisplayName = "Ticket Keys / Numbers",
                    Type = ReportParameterType.TextArea,
                    IsRequired = false,
                    Description = "Optional. Limit token generation to the selected ticket set."
                },
                new ReportParameterDefinition
                {
                    Key = "detail_fields",
                    DisplayName = "Ticket Detail Fields",
                    Type = ReportParameterType.MultiSelect,
                    IsRequired = false,
                    Description = "Controls the ticket table/list tokens that are generated.",
                    Options = SupportedDetailFields
                }
            ];
        }
    }

    public async Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        if (!request.Parameters.TryGetValue("template_id", out var templateOption) || string.IsNullOrWhiteSpace(templateOption))
            return ReportResult.Failure("Required parameter 'template_id' is missing.");

        if (!request.Parameters.TryGetValue("itsm_source", out var itsmSource) || string.IsNullOrWhiteSpace(itsmSource))
            return ReportResult.Failure("Required parameter 'itsm_source' is missing.");

        if (!request.Parameters.TryGetValue("company", out var company) || string.IsNullOrWhiteSpace(company))
            return ReportResult.Failure("Required parameter 'company' is missing.");

        if (!request.Parameters.TryGetValue("as_of_date", out var asOfDateRaw) || !DateTime.TryParse(asOfDateRaw, out var asOfDate))
            return ReportResult.Failure("Required parameter 'as_of_date' is missing or invalid.");

        var templateId = ParseTemplateId(templateOption);
        var template = await _templateStore.GetTemplateContentAsync(templateId, ct);
        if (template is null)
            return ReportResult.Failure("The selected template could not be loaded.");

        request.Parameters.TryGetValue("output_mode", out var outputMode);
        outputMode = string.IsNullOrWhiteSpace(outputMode) ? OutputModeAuto : outputMode.Trim();

        request.Parameters.TryGetValue("ticket_keys", out var ticketKeysRaw);
        request.Parameters.TryGetValue("detail_fields", out var detailFieldsRaw);
        var detailFields = ParseMulti(detailFieldsRaw, SupportedDetailFields);

        try
        {
            var allTickets = (await _dataService.GetTicketsAsync(itsmSource, company, asOfDate)).ToList();
            var requestedTicketIds = ParseTicketIds(ticketKeysRaw);
            var selectedTickets = requestedTicketIds.Count == 0
                ? allTickets.OrderBy(t => t.TicketKey, StringComparer.OrdinalIgnoreCase).ToList()
                : allTickets
                    .Where(t => requestedTicketIds.Contains(t.TicketKey)
                        || requestedTicketIds.Contains(TicketFieldHelpers.GetFieldValue(t, "Number", fallback: t.TicketKey)))
                    .OrderBy(t => t.TicketKey, StringComparer.OrdinalIgnoreCase)
                    .ToList();

            var tokens = TemplateReportTokenBuilder.Build(itsmSource, company, asOfDate, selectedTickets, detailFields, request.RequestedBy);
            var renderedBytes = TemplateTokenRenderer.RenderBytes(template.FileContent, template.Metadata.Extension, tokens);
            var renderedText = ReportTemplateContentTypeMapper.IsTextLike(template.Metadata.Extension)
                ? Encoding.UTF8.GetString(renderedBytes)
                : null;

            return BuildResult(template.Metadata, renderedBytes, renderedText, tokens, outputMode, company, asOfDate);
        }
        catch (Exception ex)
        {
            return ReportResult.Failure($"Failed to fill template: {ex.Message}");
        }
    }

    private static ReportResult BuildResult(
        ReportTemplateMetadata metadata,
        byte[] renderedBytes,
        string? renderedText,
        IReadOnlyDictionary<string, string?> tokens,
        string? outputMode,
        string company,
        DateTime asOfDate)
    {
        var extension = metadata.Extension;
        var normalizedOutputMode = string.IsNullOrWhiteSpace(outputMode) ? OutputModeAuto : outputMode;

        if (string.Equals(normalizedOutputMode, OutputModeDownloadEml, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(normalizedOutputMode, OutputModeAuto, StringComparison.OrdinalIgnoreCase)
                && metadata.Kind == ReportTemplateKind.Email
                && (extension.Equals(".html", StringComparison.OrdinalIgnoreCase) || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))))
        {
            var body = renderedText ?? string.Empty;
            var subjectTemplate = string.IsNullOrWhiteSpace(metadata.SubjectTemplate)
                ? "UPMS " + metadata.DisplayName + " - {{company}} - {{as_of_date}}"
                : metadata.SubjectTemplate!;
            var subject = TemplateTokenRenderer.RenderText(subjectTemplate, tokens);
            var emlBytes = EmailDraftBuilder.BuildEml(subject, body);

            return new ReportResult
            {
                Success = true,
                OutputType = ReportOutputType.FileDownload,
                FileName = $"{SanitizeFilePart(metadata.DisplayName)}_{SanitizeFilePart(company)}_{asOfDate:yyyy-MM-dd}.eml",
                ContentType = "message/rfc822",
                FileContent = emlBytes
            };
        }

        if ((extension.Equals(".html", StringComparison.OrdinalIgnoreCase) || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase))
            && !string.Equals(normalizedOutputMode, OutputModeDownloadFile, StringComparison.OrdinalIgnoreCase))
        {
            return new ReportResult
            {
                Success = true,
                OutputType = ReportOutputType.HtmlContent,
                HtmlContent = renderedText
            };
        }

        if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            && string.Equals(normalizedOutputMode, OutputModePreviewHtml, StringComparison.OrdinalIgnoreCase) == false
            && string.Equals(normalizedOutputMode, OutputModeDownloadFile, StringComparison.OrdinalIgnoreCase) == false
            && string.Equals(normalizedOutputMode, OutputModeDownloadEml, StringComparison.OrdinalIgnoreCase) == false)
        {
            return new ReportResult
            {
                Success = true,
                OutputType = ReportOutputType.PlainText,
                HtmlContent = renderedText
            };
        }

        return new ReportResult
        {
            Success = true,
            OutputType = ReportOutputType.FileDownload,
            FileName = $"{SanitizeFilePart(metadata.DisplayName)}_{asOfDate:yyyy-MM-dd}{metadata.Extension}",
            ContentType = metadata.ContentType,
            FileContent = renderedBytes
        };
    }

    private static string ParseTemplateId(string selectedOption)
    {
        var separatorIndex = selectedOption.IndexOf('|');
        return separatorIndex >= 0
            ? selectedOption[..separatorIndex].Trim()
            : selectedOption.Trim();
    }

    private static List<string> ParseMulti(string? raw, IReadOnlyList<string> supported)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return [];

        var supportedSet = new HashSet<string>(supported, StringComparer.OrdinalIgnoreCase);
        return raw
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(supportedSet.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static HashSet<string> ParseTicketIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return new HashSet<string>(
            raw.Split([',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string SanitizeFilePart(string value)
    {
        StringBuilder sb = new(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
                sb.Append(ch);
            else if (char.IsWhiteSpace(ch))
                sb.Append('_');
        }

        return sb.Length == 0 ? "template" : sb.ToString();
    }
}
