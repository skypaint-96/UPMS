namespace UPMS.Web.Plugins.Email;

using UPMS.Data;

/// <summary>
/// Reporting plugin that generates HTML email notifications from pre-defined templates.
/// Can either preview the rendered HTML or download an .eml draft ready for To/Cc editing.
/// </summary>
public class EmailNotificationPlugin : IReportPlugin
{
    public const string OutputModePreviewHtml = "Preview HTML";
    public const string OutputModeDownloadEml = "Download EML Draft";

    private readonly TicketDataServiceInstance _dataService;

    public EmailNotificationPlugin(TicketDataServiceInstance dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public string PluginId => "email-notification";
    public string DisplayName => "Email Notification";
    public string Description => "Generates HTML email notifications using pre-defined templates populated with ticket data. Supports browser preview or downloadable .eml drafts.";

    public IReadOnlyList<ReportParameterDefinition> Parameters =>
    [
        new ReportParameterDefinition
        {
            Key = "template",
            DisplayName = "Email Template",
            Type = ReportParameterType.Select,
            IsRequired = true,
            Description = "Choose the email template to use",
            Options = [EmailTemplateRenderer.TemplateSummary, EmailTemplateRenderer.TemplateEscalation]
        },
        new ReportParameterDefinition
        {
            Key = "output_mode",
            DisplayName = "Output Mode",
            Type = ReportParameterType.Select,
            IsRequired = false,
            Description = "Preview the email in the browser or download a draft .eml file.",
            Options = [OutputModePreviewHtml, OutputModeDownloadEml]
        },
        new ReportParameterDefinition
        {
            Key = "to",
            DisplayName = "To",
            Type = ReportParameterType.Text,
            IsRequired = false,
            Description = "Optional. Leave blank if you want to type recipients in your mail client later."
        },
        new ReportParameterDefinition
        {
            Key = "cc",
            DisplayName = "Cc",
            Type = ReportParameterType.Text,
            IsRequired = false,
            Description = "Optional. Leave blank if you want to type recipients in your mail client later."
        },
        new ReportParameterDefinition
        {
            Key = "itsm_source",
            DisplayName = "ITSM Source",
            Type = ReportParameterType.ItsmSource,
            IsRequired = true,
            Description = "Select the ITSM source to run this report against."
        },
        new ReportParameterDefinition
        {
            Key = "company",
            DisplayName = "Company",
            Type = ReportParameterType.Text,
            IsRequired = true,
            Placeholder = "Start typing a company name",
            CanonicalFieldName = "Company"
        },
        new ReportParameterDefinition
        {
            Key = "as_of_date",
            DisplayName = "As Of Date",
            Type = ReportParameterType.Date,
            IsRequired = true
        }
    ];

    public async Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        if (!request.Parameters.TryGetValue("template", out string? templateName) || string.IsNullOrWhiteSpace(templateName))
            return ReportResult.Failure("Required parameter 'template' is missing.");
        if (!request.Parameters.TryGetValue("itsm_source", out string? itsmSource) || string.IsNullOrWhiteSpace(itsmSource))
            return ReportResult.Failure("Required parameter 'itsm_source' is missing.");
        if (!request.Parameters.TryGetValue("company", out string? company) || string.IsNullOrWhiteSpace(company))
            return ReportResult.Failure("Required parameter 'company' is missing.");
        if (!request.Parameters.TryGetValue("as_of_date", out string? asOfDateStr) || !DateTime.TryParse(asOfDateStr, out DateTime asOfDate))
            return ReportResult.Failure("Required parameter 'as_of_date' is missing or invalid.");

        request.Parameters.TryGetValue("output_mode", out var outputMode);
        request.Parameters.TryGetValue("to", out var to);
        request.Parameters.TryGetValue("cc", out var cc);
        outputMode = string.IsNullOrWhiteSpace(outputMode) ? OutputModePreviewHtml : outputMode.Trim();

        try
        {
            IEnumerable<Ticket> tickets = await _dataService.GetTicketsAsync(itsmSource, company, asOfDate);
            List<Ticket> ticketList = tickets.ToList();
            string html = EmailTemplateRenderer.Render(templateName, company, itsmSource, asOfDate, ticketList);

            if (string.Equals(outputMode, OutputModeDownloadEml, StringComparison.OrdinalIgnoreCase))
            {
                var subject = EmailDraftBuilder.BuildSubject(templateName, company, itsmSource, asOfDate, ticketList);
                var emlBytes = EmailDraftBuilder.BuildEml(subject, html, to, cc);
                var safeCompany = SanitizeFilePart(company);

                return new ReportResult
                {
                    Success = true,
                    OutputType = ReportOutputType.FileDownload,
                    FileName = $"UPMS_Email_{safeCompany}_{asOfDate:yyyy-MM-dd}.eml",
                    ContentType = "message/rfc822",
                    FileContent = emlBytes
                };
            }

            return new ReportResult
            {
                Success = true,
                OutputType = ReportOutputType.HtmlContent,
                HtmlContent = html
            };
        }
        catch (Exception ex)
        {
            return ReportResult.Failure($"Failed to generate email: {ex.Message}");
        }
    }

    private static string SanitizeFilePart(string value)
    {
        var safe = new string(value.Where(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "company" : safe;
    }
}
