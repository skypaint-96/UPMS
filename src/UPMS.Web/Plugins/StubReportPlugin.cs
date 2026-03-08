namespace UPMS.Web.Plugins;

/// <summary>
/// A no-op stub plugin used for testing the plugin infrastructure.
/// </summary>
public class StubReportPlugin : IReportPlugin
{
    public string PluginId => "stub-plugin";
    public string DisplayName => "Stub Report";
    public string Description => "A no-op stub plugin for testing.";

    public IReadOnlyList<ReportParameterDefinition> Parameters =>
    [
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
            Key = "from_date",
            DisplayName = "From Date",
            Type = ReportParameterType.Date,
            IsRequired = true
        }
    ];

    public Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default)
    {
        if (!request.Parameters.ContainsKey("company"))
        {
            return Task.FromResult(ReportResult.Failure("Missing required parameter: company"));
        }

        return Task.FromResult(new ReportResult
        {
            Success = true,
            OutputType = ReportOutputType.PlainText,
            HtmlContent = $"<p>Stub report for {request.Parameters["company"]}</p>"
        });
    }
}
