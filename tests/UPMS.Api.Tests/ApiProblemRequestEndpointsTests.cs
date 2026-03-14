namespace UPMS.Api.Tests;

using System.Net;
using System.Net.Http.Json;
using UPMS.Api.Endpoints;

[TestFixture]
public sealed class ApiProblemRequestEndpointsTests
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
    public async Task Create_problem_request_returns_created_request()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/problem-requests", new
        {
            requesterName = "Alex Morgan",
            requesterEmail = "alex.morgan@example.com",
            requesterTeam = "Operations",
            companyName = "Contoso",
            itsmSource = "servicenow-prod",
            title = "Recurring payment job failures",
            description = "Multiple incidents indicate the same overnight payment batch is failing.",
            justification = "The issue is cross-team, recurring, and likely needs problem review.",
            assignee = "problem.coordinator"
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await response.Content.ReadFromJsonAsync<ProblemRequestDetailResponse>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Status, Is.EqualTo("New"));
        Assert.That(created.RequesterName, Is.EqualTo("Alex Morgan"));
    }

    [Test]
    public async Task Create_problem_request_validates_required_fields()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/problem-requests", new
        {
            requesterName = "Alex Morgan",
            title = "",
            description = "",
            justification = ""
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("Title is required"));
    }

    [Test]
    public async Task Patch_problem_request_updates_triage_state()
    {
        var created = await CreateProblemRequestAsync();

        var response = await _client.PatchAsJsonAsync($"/api/v1/problem-requests/{created.Id}", new
        {
            status = "Accepted",
            assignee = "problem.coordinator",
            decisionReason = "Recurring pattern confirmed."
        });

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<ProblemRequestDetailResponse>();

        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Status, Is.EqualTo("Accepted"));
        Assert.That(updated.Assignee, Is.EqualTo("problem.coordinator"));
    }

    [Test]
    public async Task Link_problem_request_sets_reference_and_converted_status()
    {
        var created = await CreateProblemRequestAsync();

        var response = await _client.PostAsJsonAsync($"/api/v1/problem-requests/{created.Id}/link", new
        {
            problemReference = "PRB000123",
            problemItsmSource = "servicenow-prod",
            problemCompanyName = "Contoso",
            problemTicketKey = "PRB000123",
            comment = "Raised after review."
        });

        response.EnsureSuccessStatusCode();
        var linked = await response.Content.ReadFromJsonAsync<ProblemRequestDetailResponse>();

        Assert.That(linked, Is.Not.Null);
        Assert.That(linked!.Status, Is.EqualTo("Converted / Linked"));
        Assert.That(linked.ProblemReference, Is.EqualTo("PRB000123"));
        Assert.That(linked.Comments, Is.Not.Empty);
    }

    private async Task<ProblemRequestDetailResponse> CreateProblemRequestAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/problem-requests", new
        {
            requesterName = "Alex Morgan",
            title = "Recurring payment job failures",
            description = "Multiple incidents indicate the same overnight payment batch is failing.",
            justification = "The issue is cross-team, recurring, and likely needs problem review."
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProblemRequestDetailResponse>())!;
    }
}
