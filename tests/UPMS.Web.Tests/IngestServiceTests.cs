namespace UPMS.Web.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using UPMS.Data;
using UPMS.Web.Services;

/// <summary>
/// Unit tests for IngestResult and SnapshotIngestService.
/// </summary>
[TestFixture]
public class IngestServiceTests
{
    // ── IngestResult.Failure factory ───────────────────────────────────────

    [Test]
    public void IngestResult_Failure_HasSuccessFalse()
    {
        var result = IngestResult.Failure("error");
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void IngestResult_Failure_HasEmptySnapshotId()
    {
        var result = IngestResult.Failure("error");
        Assert.That(result.SnapshotId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void IngestResult_Failure_HasZeroTicketsIngested()
    {
        var result = IngestResult.Failure("error");
        Assert.That(result.TicketsIngested, Is.EqualTo(0));
    }

    [Test]
    public void IngestResult_Failure_HasErrorMessage()
    {
        var result = IngestResult.Failure("error message");
        Assert.That(result.ErrorMessage, Is.EqualTo("error message"));
    }

    [Test]
    public void IngestResult_Failure_HasEmptyWarnings()
    {
        var result = IngestResult.Failure("error");
        Assert.That(result.Warnings, Is.Empty);
    }

    [Test]
    public void IngestResult_Success_CanBeCreated()
    {
        // Arrange
        var snapshotId = Guid.NewGuid();

        // Act
        var result = new IngestResult
        {
            Success = true,
            SnapshotId = snapshotId,
            TicketsIngested = 5,
            FieldChangesRecorded = 20
        };

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.SnapshotId, Is.EqualTo(snapshotId));
        Assert.That(result.TicketsIngested, Is.EqualTo(5));
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(20));
    }

    // ── SnapshotIngestService real ingest behaviour ────────────────────────

    [Test]
    [NonParallelizable]
    public async Task SnapshotIngestService_IngestCsvAsync_WithValidCsv_ReturnsSuccess()
    {
        // Arrange
        var (service, _) = BuildServiceWithSqlite();

        const string csv = """
            ticket_key,field_name,field_value
            INC0001234,incident_state,Open
            INC0001234,assigned_to,john.smith
            INC0001234,short_description,Server is down
            INC0001235,incident_state,In Progress
            INC0001235,assigned_to,jane.doe
            INC0001235,short_description,DB unreachable
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));

        // Act
        IngestResult result = await service.IngestCsvAsync(
            stream, "servicenow", DateTime.UtcNow, "tester", "AcmeCorp");

        // Assert
        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.TicketsIngested, Is.EqualTo(2));
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(6));
    }

    [Test]
    [NonParallelizable]
    public async Task SnapshotIngestService_IngestJsonAsync_WithValidJson_ReturnsSuccess()
    {
        // Arrange
        var (service, _) = BuildServiceWithSqlite();

        const string json = """
            [
              {
                "ticket_key": "INC0001234",
                "fields": {
                  "incident_state": "Open",
                  "assigned_to": "john.smith"
                }
              },
              {
                "ticket_key": "INC0001235",
                "fields": {
                  "incident_state": "In Progress",
                  "assigned_to": "jane.doe"
                }
              }
            ]
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        // Act
        IngestResult result = await service.IngestJsonAsync(
            stream, "servicenow", DateTime.UtcNow, "tester", "AcmeCorp");

        // Assert
        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.TicketsIngested, Is.EqualTo(2));
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(4));
    }

    // ── SnapshotIngestService constructor guards ───────────────────────────

    [Test]
    public void SnapshotIngestService_RequiresDataService_ThrowsOnNull()
    {
        var fakeMappingService = new FakeMappingService();

        Assert.Throws<ArgumentNullException>(() =>
            new SnapshotIngestService(null!, fakeMappingService));
    }

    [Test]
    public void SnapshotIngestService_RequiresMappingService_ThrowsOnNull()
    {
        var fakeOptions = Options.Create(new DatabaseOptions { ConnectionString = "Host=localhost;Database=test;" });
        var fakeDataService = new TicketDataServiceInstance(fakeOptions);

        Assert.Throws<ArgumentNullException>(() =>
            new SnapshotIngestService(fakeDataService, null!));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a SnapshotIngestService backed by a fresh SQLite in-memory database.
    /// Returns both the service and the connection (keep connection open for lifetime of test).
    /// </summary>
    private static (SnapshotIngestService Service, SqliteConnection Connection) BuildServiceWithSqlite()
    {
        // Use a named in-memory database so the schema is shared across connections
        var dbName = $"ingest_test_{Guid.NewGuid():N}";
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        var connection = new SqliteConnection(connectionString);
        connection.Open();

        // Create schema
        using var cmd = connection.CreateCommand();
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

        // Wire TicketDataService to use this SQLite connection
        TicketDataService.Initialize(() => new SqliteConnection(connectionString));

        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });

        // TicketDataServiceInstance calls Initialize internally; we override immediately after
        var dataServiceInstance = new TicketDataServiceInstance(options);
        // Re-apply SQLite factory since the instance constructor sets a Npgsql factory
        TicketDataService.Initialize(() => new SqliteConnection(connectionString));

        var fakeMappingService = new FakeMappingService();
        var service = new SnapshotIngestService(dataServiceInstance, fakeMappingService);

        return (service, connection);
    }

    private class FakeMappingService : IItsmFieldMappingService
    {
        public string GetCanonicalName(string itsmSource, string sourceFieldName) => sourceFieldName;
        public IEnumerable<ItsmFieldMapping> GetMappingsForSource(string itsmSource) => [];
        public Task UpsertMappingAsync(string itsmSource, string sourceFieldName, string canonicalFieldName) => Task.CompletedTask;
    }
}
