namespace UPMS.Web.Plugins.PowerPoint;

using UPMS.Data;

/// <summary>
/// Reporting plugin that generates a minimal PowerPoint (.pptx) slide deck
/// summarising ticket data for a company over a date range.
/// </summary>
public class PowerPointReportPlugin : IReportPlugin
{
    private readonly TicketDataServiceInstance _dataService;

    public PowerPointReportPlugin(TicketDataServiceInstance dataService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
    }

    public string PluginId => "powerpoint-report";
    public string DisplayName => "PowerPoint Report Pack";
    public string Description => "Generates a PowerPoint slide deck summarising ticket data for a company and date range.";

    public IReadOnlyList<ReportParameterDefinition> Parameters =>
    [
        new ReportParameterDefinition
        {
            Key = "itsm_source",
            DisplayName = "ITSM Source",
            Type = ReportParameterType.Select,
            IsRequired = true,
            Options = ["servicenow", "jira"]
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
            Key = "title_style",
            DisplayName = "Title Style",
            Type = ReportParameterType.Select,
            IsRequired = false,
            Description = "Visual style for the title slide",
            Options = ["Standard", "Executive", "Minimal"]
        }
    ];

    public async Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        // Validate required parameters
        if (!request.Parameters.TryGetValue("itsm_source", out string? itsmSource) || string.IsNullOrWhiteSpace(itsmSource))
            return ReportResult.Failure("Required parameter 'itsm_source' is missing.");
        if (!request.Parameters.TryGetValue("company", out string? company) || string.IsNullOrWhiteSpace(company))
            return ReportResult.Failure("Required parameter 'company' is missing.");
        if (!request.Parameters.TryGetValue("as_of_date", out string? asOfDateStr) || !DateTime.TryParse(asOfDateStr, out DateTime asOfDate))
            return ReportResult.Failure("Required parameter 'as_of_date' is missing or invalid.");

        request.Parameters.TryGetValue("title_style", out string? titleStyle);
        titleStyle ??= "Standard";

        try
        {
            IEnumerable<Ticket> tickets = await _dataService.GetTicketsAsync(itsmSource, company, asOfDate);
            List<Ticket> ticketList = tickets.ToList();

            byte[] pptxBytes = PowerPointGenerator.Generate(company, itsmSource, asOfDate, titleStyle, ticketList);

            string fileName = $"UPMS_Report_{company.Replace(" ", "_")}_{asOfDate:yyyy-MM-dd}.pptx";

            return new ReportResult
            {
                Success = true,
                OutputType = ReportOutputType.FileDownload,
                FileName = fileName,
                ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                FileContent = pptxBytes
            };
        }
        catch (Exception ex)
        {
            return ReportResult.Failure($"Failed to generate report: {ex.Message}");
        }
    }
}
