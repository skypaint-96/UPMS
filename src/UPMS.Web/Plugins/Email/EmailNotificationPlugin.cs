namespace UPMS.Web.Plugins.Email;

using UPMS.Data;

/// <summary>
/// Reporting plugin that generates HTML email notifications from pre-defined templates.
/// </summary>
public class EmailNotificationPlugin : IReportPlugin
{
    private readonly TicketDataServiceInstance _dataService;

    public EmailNotificationPlugin(TicketDataServiceInstance dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public string PluginId => "email-notification";
    public string DisplayName => "Email Notification";
    public string Description => "Generates HTML email notifications using pre-defined templates populated with ticket data.";

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
            IsRequired = true
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

        try
        {
            IEnumerable<Ticket> tickets = await _dataService.GetTicketsAsync(itsmSource, company, asOfDate);
            List<Ticket> ticketList = tickets.ToList();
            string html = EmailTemplateRenderer.Render(templateName, company, itsmSource, asOfDate, ticketList);

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
}
