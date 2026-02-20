namespace UPMS.Web.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using UPMS.Data;
using UPMS.Web.Plugins;
using UPMS.Web.Plugins.PowerPoint;

/// <summary>
/// Unit tests for the PowerPoint report plugin (Stage 6).
/// </summary>
[TestFixture]
public class PowerPointPluginTests
{
    [Test]
    public void PowerPointPlugin_ImplementsIReportPlugin()
    {
        TicketDataServiceInstance dataService = CreateTestDataService();
        PowerPointReportPlugin plugin = new(dataService);
        Assert.That(plugin, Is.InstanceOf<IReportPlugin>());
    }

    [Test]
    public void PowerPointPlugin_HasPluginId()
    {
        TicketDataServiceInstance dataService = CreateTestDataService();
        PowerPointReportPlugin plugin = new(dataService);
        Assert.That(plugin.PluginId, Is.EqualTo("powerpoint-report"));
    }

    [Test]
    [NonParallelizable]
    public async Task PowerPointPlugin_HasPptxOutputType()
    {
        (TicketDataServiceInstance dataService, SqliteConnection conn) = CreateTestDataServiceWithConnection();
        using (conn)
        {
            PowerPointReportPlugin plugin = new(dataService);
            ReportRequest request = new()
            {
                PluginId = plugin.PluginId,
                Parameters = new Dictionary<string, string>
                {
                    ["itsm_source"] = "servicenow",
                    ["company"] = "Acme",
                    ["as_of_date"] = "2025-01-15"
                },
                RequestedBy = "test",
                RequestedAt = DateTime.UtcNow
            };

            ReportResult result = await plugin.GenerateAsync(request);

            Assert.That(result.OutputType, Is.EqualTo(ReportOutputType.FileDownload));
        }
    }

    [Test]
    public void PowerPointPlugin_Parameters_ContainsCompanyParam()
    {
        TicketDataServiceInstance dataService = CreateTestDataService();
        PowerPointReportPlugin plugin = new(dataService);
        Assert.That(plugin.Parameters.Any(p => p.Key == "company"), Is.True);
    }

    [Test]
    public void PowerPointPlugin_Parameters_ContainsDateRangeParam()
    {
        TicketDataServiceInstance dataService = CreateTestDataService();
        PowerPointReportPlugin plugin = new(dataService);
        ReportParameterDefinition? dateParam = plugin.Parameters.FirstOrDefault(p => p.Key == "as_of_date");
        Assert.That(dateParam, Is.Not.Null);
        Assert.That(dateParam!.Type, Is.EqualTo(ReportParameterType.Date));
    }

    [Test]
    [NonParallelizable]
    public async Task PowerPointPlugin_GenerateAsync_ReturnsPptxFile()
    {
        (TicketDataServiceInstance dataService, SqliteConnection conn) = CreateTestDataServiceWithConnection();
        using (conn)
        {
            PowerPointReportPlugin plugin = new(dataService);
            ReportRequest request = new()
            {
                PluginId = plugin.PluginId,
                Parameters = new Dictionary<string, string>
                {
                    ["itsm_source"] = "servicenow",
                    ["company"] = "Acme",
                    ["as_of_date"] = "2025-01-15"
                },
                RequestedBy = "test",
                RequestedAt = DateTime.UtcNow
            };

            ReportResult result = await plugin.GenerateAsync(request);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.FileContent, Is.Not.Null);
            Assert.That(result.FileContent!.Length, Is.GreaterThan(0));
            Assert.That(result.FileName, Does.EndWith(".pptx"));
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="TicketDataServiceInstance"/> backed by a fresh SQLite in-memory database.
    /// </summary>
    private static TicketDataServiceInstance CreateTestDataService()
    {
        (TicketDataServiceInstance svc, _) = CreateTestDataServiceWithConnection();
        return svc;
    }

    private static (TicketDataServiceInstance Service, SqliteConnection Connection) CreateTestDataServiceWithConnection()
    {
        string dbName = $"pptx_test_{Guid.NewGuid():N}";
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

        TicketDataService.Initialize(() => new SqliteConnection(connectionString));

        IOptions<DatabaseOptions> options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        TicketDataServiceInstance dataService = new(options);
        TicketDataService.Initialize(() => new SqliteConnection(connectionString));

        return (dataService, connection);
    }
}
