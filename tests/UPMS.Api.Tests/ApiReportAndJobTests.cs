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
    public async Task Report_plugin_catalog_lists_example_plugins()
    {
        var response = await _client.GetAsync("/api/v1/reports/plugins");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("example-ticket-csv-export"));
    }

    [Test]
    public async Task Report_job_endpoint_accepts_valid_job_request()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/jobs/report-execution", new
        {
            pluginId = "example-ticket-csv-export",
            parameters = new Dictionary<string, string>
            {
                ["itsm_source"] = "servicenow-prod",
                ["company"] = "Contoso",
                ["as_of_date"] = "2026-03-02"
            }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
    }
}
