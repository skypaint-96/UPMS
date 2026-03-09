namespace UPMS.Api.Tests;

using System.Net;
using System.Text.Json;

[TestFixture]
public sealed class ApiRouteSmokeTests
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
    public async Task Health_endpoint_returns_ok()
    {
        var response = await _client.GetAsync("/api/v1/health");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Canonical_fields_endpoint_returns_seeded_catalog()
    {
        var response = await _client.GetAsync("/api/v1/canonical-fields");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("Number"));
        Assert.That(payload, Does.Contain("Company"));
    }
}
