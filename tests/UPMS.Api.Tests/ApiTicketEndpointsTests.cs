namespace UPMS.Api.Tests;

using System.Net;
using UPMS.Data;

[TestFixture]
public sealed class ApiTicketEndpointsTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private readonly string _ticketKey = TicketKeyFactory.Compose("servicenow-prod", "Contoso", "PRB0001");

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
    public async Task Ticket_collection_endpoint_returns_ticket_as_of_query_time()
    {
        var response = await _client.GetAsync("/api/v1/tickets?itsmSource=servicenow-prod&company=Contoso&asOf=2026-03-02T00:00:00Z");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain(_ticketKey));
        Assert.That(payload, Does.Contain("Investigating"));
    }

    [Test]
    public async Task Ticket_history_endpoint_returns_field_changes()
    {
        var response = await _client.GetAsync($"/api/v1/tickets/Contoso/{Uri.EscapeDataString(_ticketKey)}/history");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = await response.Content.ReadAsStringAsync();
        Assert.That(payload, Does.Contain("State"));
    }
}
