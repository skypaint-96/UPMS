namespace UPMS.Api.Tests;

using System.Net;
using System.Net.Http.Json;

[TestFixture]
public sealed class ApiReportAndJobTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task Report_plugin_catalog_lists_template_runner_only()
    {
        var response = await _client.GetAsync("/api/v1/reports/plugins");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("tokenised-template-report"));
        Assert.That(payload, Does.Not.Contain("example-ticket-csv-export"));
    }

    [Test]
    public async Task Fresh_state_report_template_catalog_contains_seeded_starters()
    {
        var response = await _client.GetAsync("/api/v1/report-templates");
        response.EnsureSuccessStatusCode();

        var templates = await response.Content.ReadFromJsonAsync<List<ReportTemplateListRow>>();
        Assert.That(templates, Is.Not.Null);
        Assert.That(templates!, Is.Not.Empty);
        Assert.That(templates.Any(template => template.IsStarterTemplate), Is.True);
        Assert.That(templates.Any(template => template.TemplateTypeId == "html-document"), Is.True);
        Assert.That(templates.Any(template => template.TemplateTypeId == "powerpoint-presentation"), Is.True);
    }

    [Test]
    public async Task Report_job_endpoint_accepts_valid_template_job_request()
    {
        var templates = await _client.GetFromJsonAsync<List<ReportTemplateListRow>>("/api/v1/report-templates");
        Assert.That(templates, Is.Not.Null);
        var starter = templates!.First(template => template.TemplateTypeId == "html-document");

        var response = await _client.PostAsJsonAsync("/api/v1/jobs/report-execution", new
        {
            pluginId = "tokenised-template-report",
            parameters = new Dictionary<string, string>
            {
                ["template_id"] = starter.Id,
                ["itsm_source"] = "servicenow-prod",
                ["company"] = "Contoso",
                ["as_of_date"] = "2026-03-02"
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
    }

    private sealed record ReportTemplateListRow(string Id, string? TemplateTypeId, bool IsStarterTemplate);
}
