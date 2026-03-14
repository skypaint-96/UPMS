namespace UPMS.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

[TestFixture]
public sealed class ApiReportTemplateScopingTests
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
    public async Task Global_template_is_available_for_any_context()
    {
        var template = await UploadTemplateAsync(UniqueName("Global"));

        Assert.That(template.Scope.IsGlobal, Is.True);

        var firstContext = await GetTemplatesForContextAsync("servicenow-prod", "Contoso");
        var secondContext = await GetTemplatesForContextAsync("jira-prod", "Tailspin");

        Assert.That(firstContext.Any(row => row.Id == template.Id), Is.True);
        Assert.That(secondContext.Any(row => row.Id == template.Id), Is.True);
    }

    [Test]
    public async Task Itsm_source_only_templates_are_filtered_by_source()
    {
        var template = await UploadTemplateAsync(
            UniqueName("ItsmOnly"),
            new
            {
                itsmSources = new[] { "servicenow-prod" }
            });

        var allowed = await GetTemplatesForContextAsync("servicenow-prod", "Contoso");
        var blocked = await GetTemplatesForContextAsync("jira-prod", "Contoso");

        Assert.That(allowed.Any(row => row.Id == template.Id), Is.True);
        Assert.That(blocked.Any(row => row.Id == template.Id), Is.False);
    }

    [Test]
    public async Task Company_only_templates_are_filtered_by_company()
    {
        var template = await UploadTemplateAsync(
            UniqueName("CompanyOnly"),
            new
            {
                companies = new[] { "Contoso" }
            });

        var allowed = await GetTemplatesForContextAsync("servicenow-prod", "Contoso");
        var blocked = await GetTemplatesForContextAsync("servicenow-prod", "Fabrikam");

        Assert.That(allowed.Any(row => row.Id == template.Id), Is.True);
        Assert.That(blocked.Any(row => row.Id == template.Id), Is.False);
    }

    [Test]
    public async Task Combined_scope_logic_supports_intersections_and_exact_pairs()
    {
        var intersectionTemplate = await UploadTemplateAsync(
            UniqueName("Intersection"),
            new
            {
                itsmSources = new[] { "servicenow-prod" },
                companies = new[] { "Contoso" }
            });

        var pairTemplate = await UploadTemplateAsync(
            UniqueName("PairOnly"),
            new
            {
                itsmSourceCompanies = new[]
                {
                    new
                    {
                        itsmSource = "servicenow-prod",
                        company = "Fabrikam"
                    }
                }
            });

        var contosoInServiceNow = await GetTemplatesForContextAsync("servicenow-prod", "Contoso");
        var fabrikamInServiceNow = await GetTemplatesForContextAsync("servicenow-prod", "Fabrikam");
        var contosoInJira = await GetTemplatesForContextAsync("jira-prod", "Contoso");

        Assert.That(contosoInServiceNow.Any(row => row.Id == intersectionTemplate.Id), Is.True);
        Assert.That(fabrikamInServiceNow.Any(row => row.Id == intersectionTemplate.Id), Is.False);
        Assert.That(contosoInJira.Any(row => row.Id == intersectionTemplate.Id), Is.False);

        Assert.That(fabrikamInServiceNow.Any(row => row.Id == pairTemplate.Id), Is.True);
        Assert.That(contosoInServiceNow.Any(row => row.Id == pairTemplate.Id), Is.False);
        Assert.That(contosoInJira.Any(row => row.Id == pairTemplate.Id), Is.False);
    }

    [Test]
    public async Task Invalid_template_usage_is_rejected_during_report_execution()
    {
        var template = await UploadTemplateAsync(
            UniqueName("ExecutionBlocked"),
            new
            {
                companies = new[] { "Contoso" }
            });

        var response = await ExecuteReportAsync(template.Id, "servicenow-prod", "Tailspin");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var payload = await response.Content.ReadFromJsonAsync<ReportExecutionRow>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Success, Is.False);
        Assert.That(payload.ErrorMessage, Does.Contain("not available"));
        Assert.That(payload.ErrorMessage, Does.Contain(template.DisplayName));
        Assert.That(payload.ErrorMessage, Does.Contain("servicenow-prod"));
        Assert.That(payload.ErrorMessage, Does.Contain("Tailspin"));
    }

    [Test]
    public async Task Invalid_template_usage_is_rejected_when_queueing_report_job()
    {
        var template = await UploadTemplateAsync(
            UniqueName("QueueBlocked"),
            new
            {
                itsmSources = new[] { "servicenow-prod" },
                companies = new[] { "Contoso" }
            });

        var response = await QueueReportAsync(template.Id, "servicenow-prod", "Tailspin");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var payload = await response.Content.ReadFromJsonAsync<ErrorRow>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Error, Does.Contain("not available"));
        Assert.That(payload.Error, Does.Contain(template.DisplayName));
        Assert.That(payload.Error, Does.Contain("Tailspin"));
    }

    private async Task<TemplateRow> UploadTemplateAsync(string displayName, object? scope = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(displayName), "displayName");
        form.Add(new StringContent("html-document"), "templateTypeId");
        form.Add(new StringContent("<html><body><h1>{{company}}</h1><p>{{ticket_count}}</p></body></html>"), "textContent");

        if (scope is not null)
            form.Add(new StringContent(JsonSerializer.Serialize(scope)), "scope");

        var response = await _client.PostAsync("/api/v1/report-templates", form);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TemplateRow>();
        Assert.That(payload, Is.Not.Null);
        return payload!;
    }

    private async Task<List<TemplateRow>> GetTemplatesForContextAsync(string itsmSource, string company)
    {
        var url = $"/api/v1/report-templates?itsmSource={Uri.EscapeDataString(itsmSource)}&company={Uri.EscapeDataString(company)}";
        var payload = await _client.GetFromJsonAsync<List<TemplateRow>>(url);
        Assert.That(payload, Is.Not.Null);
        return payload!;
    }

    private Task<HttpResponseMessage> ExecuteReportAsync(string templateId, string itsmSource, string company)
    {
        return _client.PostAsJsonAsync("/api/v1/reports/execute", new
        {
            pluginId = "tokenised-template-report",
            parameters = new Dictionary<string, string>
            {
                ["template_id"] = templateId,
                ["itsm_source"] = itsmSource,
                ["company"] = company,
                ["as_of_date"] = "2026-03-01"
            }
        });
    }

    private Task<HttpResponseMessage> QueueReportAsync(string templateId, string itsmSource, string company)
    {
        return _client.PostAsJsonAsync("/api/v1/jobs/report-execution", new
        {
            pluginId = "tokenised-template-report",
            parameters = new Dictionary<string, string>
            {
                ["template_id"] = templateId,
                ["itsm_source"] = itsmSource,
                ["company"] = company,
                ["as_of_date"] = "2026-03-01"
            }
        });
    }

    private static string UniqueName(string prefix)
        => $"{prefix}-{Guid.NewGuid():N}";

    private sealed record ScopePairRow(string ItsmSource, string Company);

    private sealed record ScopeRow(
        bool IsGlobal,
        List<string> ItsmSources,
        List<string> Companies,
        List<ScopePairRow> ItsmSourceCompanies);

    private sealed record TemplateRow(
        string Id,
        string DisplayName,
        string? TemplateTypeId,
        bool IsStarterTemplate,
        ScopeRow Scope);

    private sealed record ReportExecutionRow(
        bool Success,
        string OutputType,
        string? FileName,
        string? ContentType,
        string? HtmlContent,
        string? ErrorMessage);

    private sealed record ErrorRow(string Error);
}
