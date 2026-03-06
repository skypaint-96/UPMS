namespace UPMS.Web.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UPMS.Data;
using UPMS.Web.Plugins;
using UPMS.Web.Plugins.Email;

/// <summary>
/// Unit tests for the email notification report plugin (Stage 7).
/// </summary>
[TestFixture]
public class EmailNotificationPluginTests
{
    [Test]
    public void EmailPlugin_ImplementsIReportPlugin()
    {
        TicketDataServiceInstance dataService = CreateTestDataService();
        EmailNotificationPlugin plugin = new(dataService);
        Assert.That(plugin, Is.InstanceOf<IReportPlugin>());
    }

    [Test]
    public void EmailPlugin_HasPluginId()
    {
        TicketDataServiceInstance dataService = CreateTestDataService();
        EmailNotificationPlugin plugin = new(dataService);
        Assert.That(plugin.PluginId, Is.EqualTo("email-notification"));
    }

    [Test]
    public void EmailPlugin_OffersMultipleTemplateOptions()
    {
        TicketDataServiceInstance dataService = CreateTestDataService();
        EmailNotificationPlugin plugin = new(dataService);
        ReportParameterDefinition? templateParam = plugin.Parameters.FirstOrDefault(p => p.Key == "template");
        Assert.That(templateParam, Is.Not.Null);
        Assert.That(templateParam!.Options, Is.Not.Null);
        Assert.That(templateParam.Options!.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void EmailPlugin_Parameters_ContainsTemplateParam()
    {
        TicketDataServiceInstance dataService = CreateTestDataService();
        EmailNotificationPlugin plugin = new(dataService);
        ReportParameterDefinition? templateParam = plugin.Parameters.FirstOrDefault(p => p.Key == "template");
        Assert.That(templateParam, Is.Not.Null);
        Assert.That(templateParam!.Type, Is.EqualTo(ReportParameterType.Select));
    }

    [Test]
    [NonParallelizable]
    public async Task EmailPlugin_GenerateAsync_ReturnsHtmlContent()
    {
        (TicketDataServiceInstance dataService, SqliteConnection conn) = CreateTestDataServiceWithConnection();
        using (conn)
        {
            EmailNotificationPlugin plugin = new(dataService);
            ReportRequest request = new()
            {
                PluginId = plugin.PluginId,
                Parameters = new Dictionary<string, string>
                {
                    ["template"] = EmailTemplateRenderer.TemplateSummary,
                    ["itsm_source"] = "servicenow",
                    ["company"] = "Acme",
                    ["as_of_date"] = "2025-01-15"
                },
                RequestedBy = "test",
                RequestedAt = DateTime.UtcNow
            };

            ReportResult result = await plugin.GenerateAsync(request);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.OutputType, Is.EqualTo(ReportOutputType.HtmlContent));
            Assert.That(result.HtmlContent, Is.Not.Null);
        }
    }

    [Test]
    [NonParallelizable]
    public async Task EmailPlugin_GenerateAsync_PopulatesTemplateWithData()
    {
        (TicketDataServiceInstance dataService, SqliteConnection conn) = CreateTestDataServiceWithConnection();
        using (conn)
        {
            EmailNotificationPlugin plugin = new(dataService);
            ReportRequest request = new()
            {
                PluginId = plugin.PluginId,
                Parameters = new Dictionary<string, string>
                {
                    ["template"] = EmailTemplateRenderer.TemplateSummary,
                    ["itsm_source"] = "servicenow",
                    ["company"] = "Acme",
                    ["as_of_date"] = "2025-01-15"
                },
                RequestedBy = "test",
                RequestedAt = DateTime.UtcNow
            };

            ReportResult result = await plugin.GenerateAsync(request);

            Assert.That(result.HtmlContent, Does.Contain("Monthly Problem Management Summary"));
            Assert.That(result.HtmlContent, Does.Contain("Acme"));
        }
    }

    // ── EmailTemplateRenderer direct tests ────────────────────────────────

    [Test]
    public void EmailTemplateRenderer_Render_SummaryTemplate_ContainsCompanyName()
    {
        string html = EmailTemplateRenderer.Render(
            EmailTemplateRenderer.TemplateSummary, "TestCo", "servicenow", DateTime.Today, []);
        Assert.That(html, Does.Contain("TestCo"));
    }

    [Test]
    public void EmailTemplateRenderer_Render_EscalationTemplate_ContainsAlert()
    {
        string html = EmailTemplateRenderer.Render(
            EmailTemplateRenderer.TemplateEscalation, "TestCo", "jira", DateTime.Today, []);
        Assert.That(html, Does.Contain("Escalation"));
    }

    [Test]
    public void EmailTemplateRenderer_Render_UnknownTemplate_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            EmailTemplateRenderer.Render("Unknown", "TestCo", "servicenow", DateTime.Today, []));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static TicketDataServiceInstance CreateTestDataService()
    {
        (TicketDataServiceInstance svc, _) = CreateTestDataServiceWithConnection();
        return svc;
    }

    private static (TicketDataServiceInstance Service, SqliteConnection Connection) CreateTestDataServiceWithConnection()
    {
        string dbName = $"email_test_{Guid.NewGuid():N}";
        string connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        SqliteConnection connection = new(connectionString);
        connection.Open();

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS raw_snapshot (
                id TEXT PRIMARY KEY,
                itsm_source TEXT NOT NULL,
                snapshot_date TEXT NOT NULL,
                uploaded_by TEXT NOT NULL,
                uploaded_at TEXT NOT NULL,
                upload_metadata TEXT);

            CREATE TABLE IF NOT EXISTS snapshot_ticket (
                id TEXT PRIMARY KEY,
                snapshot_id TEXT NOT NULL,
                company_name TEXT NOT NULL,
                ticket_key TEXT NOT NULL,
                UNIQUE(snapshot_id, ticket_key));

            CREATE TABLE IF NOT EXISTS field_change (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                company_name TEXT NOT NULL,
                ticket_key TEXT NOT NULL,
                field_name TEXT NOT NULL,
                field_value TEXT,
                observed_at TEXT NOT NULL,
                snapshot_id TEXT NOT NULL);

            CREATE TABLE IF NOT EXISTS itsm_field_mapping (
                itsm_source TEXT NOT NULL,
                source_field_name TEXT NOT NULL,
                canonical_field_name TEXT NOT NULL,
                PRIMARY KEY(itsm_source, source_field_name));
            """;
        cmd.ExecuteNonQuery();

        var optionsBuilder = new DbContextOptionsBuilder<UpmsDbContext>();
        optionsBuilder.UseSqlite(connection);
        var db = new UpmsDbContext(optionsBuilder.Options);

        var ticketSvc = new TicketDataService(db);
        var dataService = new TicketDataServiceInstance(ticketSvc);

        return (dataService, connection);
    }
}
