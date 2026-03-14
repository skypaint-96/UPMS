namespace UPMS.Api.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
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

    [Test]
    public async Task Bulk_snapshot_job_endpoint_accepts_multiple_files_and_returns_jobs()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("servicenow-prod"), "itsmSource");
        form.Add(new StringContent("2026-03-02"), "snapshotDates");
        form.Add(new StringContent("2026-03-03"), "snapshotDates");

        var firstFile = new ByteArrayContent(Encoding.UTF8.GetBytes("number,company,state\nPRB0002,Contoso,Open\n"));
        firstFile.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(firstFile, "files", "snapshot-2026-03-02.csv");

        var secondFile = new ByteArrayContent(Encoding.UTF8.GetBytes("number,company,state\nPRB0003,Contoso,Closed\n"));
        secondFile.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(secondFile, "files", "snapshot-2026-03-03.csv");

        var response = await _client.PostAsync("/api/v1/jobs/snapshot-ingest/bulk", form);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

        var payload = await response.Content.ReadFromJsonAsync<BulkQueuedJobsResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.QueuedCount, Is.EqualTo(2));
        Assert.That(payload.Jobs, Has.Count.EqualTo(2));
        Assert.That(payload.Jobs.All(job => job.JobType == "snapshot-ingest"), Is.True);
    }


    [Test]
    public async Task Distribution_list_endpoints_support_crud_and_company_lookup()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/distribution-lists", new
        {
            companyName = "Contoso",
            name = "Leadership",
            description = "Primary recipients",
            isActive = true,
            recipients = new[]
            {
                new
                {
                    channel = "email",
                    endpoint = "leader@example.com",
                    isActive = true,
                    sortOrder = 0
                }
            }
        });

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<DistributionListRow>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.CompanyName, Is.EqualTo("Contoso"));
        Assert.That(created.RecipientCount, Is.EqualTo(1));

        var companies = await _client.GetFromJsonAsync<List<CompanyProfileRow>>("/api/v1/company-profiles");
        Assert.That(companies, Is.Not.Null);
        Assert.That(companies!.Single().DistributionListCount, Is.EqualTo(1));

        var lists = await _client.GetFromJsonAsync<List<DistributionListRow>>("/api/v1/distribution-lists?company=Contoso");
        Assert.That(lists, Is.Not.Null);
        Assert.That(lists!, Has.Count.EqualTo(1));

        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/distribution-lists/{created.Id}", new
        {
            companyName = "Contoso",
            name = "Leadership",
            description = "Updated recipients",
            isActive = true,
            recipients = new[]
            {
                new
                {
                    channel = "email",
                    endpoint = "exec@example.com",
                    isActive = true,
                    sortOrder = 0
                },
                new
                {
                    channel = "email",
                    endpoint = "ops@example.com",
                    isActive = true,
                    sortOrder = 1
                }
            }
        });

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await updateResponse.Content.ReadFromJsonAsync<DistributionListRow>();
        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.RecipientCount, Is.EqualTo(2));

        var deleteResponse = await _client.DeleteAsync($"/api/v1/distribution-lists/{created.Id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var afterDelete = await _client.GetFromJsonAsync<List<DistributionListRow>>("/api/v1/distribution-lists?company=Contoso");
        Assert.That(afterDelete, Is.Not.Null);
        Assert.That(afterDelete!, Is.Empty);
    }

    [Test]
    public async Task Report_job_endpoint_accepts_distribution_list_selection()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/v1/distribution-lists", new
        {
            companyName = "Contoso",
            name = "Leadership",
            isActive = true,
            recipients = new[]
            {
                new
                {
                    channel = "email",
                    endpoint = "leader@example.com",
                    isActive = true,
                    sortOrder = 0
                }
            }
        });
        createResponse.EnsureSuccessStatusCode();
        var list = await createResponse.Content.ReadFromJsonAsync<DistributionListRow>();

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
            },
            distributionListIds = new[] { list!.Id }
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
    }

    private sealed record ReportTemplateListRow(string Id, string? TemplateTypeId, bool IsStarterTemplate);

    private sealed record CompanyProfileRow(Guid Id, string CompanyKey, string DisplayName, int DistributionListCount);

    private sealed record DistributionListRow(Guid Id, string CompanyName, string Name, int RecipientCount);

    private sealed record BackgroundJobRow(Guid Id, string JobType, string Status);

    private sealed record BulkQueuedJobsResponse(int QueuedCount, List<BackgroundJobRow> Jobs);
}
