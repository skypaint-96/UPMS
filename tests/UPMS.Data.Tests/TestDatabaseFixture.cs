namespace UPMS.Data.Tests;

using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using UPMS.Data;

public class TestDatabaseFixture
{
    // Use an in-memory SQLite database for tests
    private static SqliteConnection? _connection;

    // Expose a connection factory for legacy tests
    public static Func<IDbConnection> ConnectionFactory => () => _connection!;

    private static bool _initialized = false;

    // Expose snapshot ids for tests
    public static string Company1OldSnapshotId { get; private set; } = string.Empty;
    public static string Company1NewSnapshotId { get; private set; } = string.Empty;
    public static string Company2SnapshotId { get; private set; } = string.Empty;
    public static string Company3SnapshotId { get; private set; } = string.Empty;

    public static async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        // Create a single in-memory connection and keep it open for the lifetime of the tests
        _connection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _connection.Open();

        // Enable foreign keys
        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            await ((System.Data.Common.DbCommand)pragma).ExecuteNonQueryAsync();
        }

        // Initialize the lower-level data service to use this connection
        TicketDataService.Initialize(() => _connection);

        await CreateSqliteTablesAsync(_connection);
        await SeedTestDataAsync(_connection);

        _initialized = true;
    }

    public static Task CleanupAsync()
    {
        _initialized = false;
        try { _connection?.Close(); } catch { }
        _connection = null;
        return Task.CompletedTask;
    }

    public static IItsmFieldMappingService GetMappingService()
    {
        return new ItsmFieldMappingService(() => _connection!);
    }

    public static IItsmSourceService GetSourceService()
    {
        return new ItsmSourceService(() => _connection!);
    }

    // ------------------------------------------------------------------
    // SQLite schema creation
    // ------------------------------------------------------------------
    private static async Task CreateSqliteTablesAsync(IDbConnection connection)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS raw_snapshot (
                id              TEXT PRIMARY KEY,
                itsm_source     TEXT NOT NULL,
                snapshot_date   DATETIME NOT NULL,
                uploaded_by     TEXT NOT NULL,
                uploaded_at     DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                upload_metadata TEXT,
                created_at      DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP)
            );

            CREATE TABLE IF NOT EXISTS snapshot_ticket (
                id              TEXT PRIMARY KEY,
                snapshot_id     TEXT NOT NULL,
                company_name    TEXT NOT NULL,
                ticket_key      TEXT NOT NULL,
                created_at      DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                CONSTRAINT uq_snapshot_ticket UNIQUE (snapshot_id, ticket_key),
                FOREIGN KEY(snapshot_id) REFERENCES raw_snapshot(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS field_change (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                company_name    TEXT NOT NULL,
                ticket_key      TEXT NOT NULL,
                field_name      TEXT NOT NULL,
                field_value     TEXT,
                observed_at     DATETIME NOT NULL,
                snapshot_id     TEXT NOT NULL,
                created_at      DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                FOREIGN KEY(snapshot_id) REFERENCES raw_snapshot(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS itsm_source (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                name          TEXT NOT NULL,
                display_label TEXT NOT NULL,
                created_at    DATETIME NOT NULL DEFAULT (CURRENT_TIMESTAMP),
                CONSTRAINT uq_itsm_source_name UNIQUE (name)
            );

            CREATE TABLE IF NOT EXISTS itsm_field_mapping (
                itsm_source             TEXT  NOT NULL,
                source_field_name       TEXT  NOT NULL,
                canonical_field_name    TEXT  NOT NULL,
                is_required             INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (itsm_source, source_field_name)
            );

            CREATE INDEX IF NOT EXISTS idx_snapshot_ticket_snapshot_id  ON snapshot_ticket (snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_ticket_company_name  ON snapshot_ticket (company_name);
            CREATE INDEX IF NOT EXISTS idx_field_change_company_ticket   ON field_change (company_name, ticket_key, observed_at);
            CREATE INDEX IF NOT EXISTS idx_raw_snapshot_itsm_source      ON raw_snapshot (itsm_source);
            CREATE INDEX IF NOT EXISTS idx_itsm_field_mapping_source     ON itsm_field_mapping (itsm_source);
            """;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await ((System.Data.Common.DbCommand)cmd).ExecuteNonQueryAsync();
    }

    // ------------------------------------------------------------------
    // Shared seed logic
    // ------------------------------------------------------------------
    private static async Task SeedTestDataAsync(IDbConnection connection)
    {
        string company1Name = TestData.Companies.Company1;
        string company2Name = TestData.Companies.Company2;
        string company3Name = TestData.Companies.Company3;

        DateTime testSnapshotDate  = TestData.Dates.NewSnapshotDate;
        DateTime olderSnapshotDate = TestData.Dates.OlderSnapshotDate;

        Company1OldSnapshotId = Guid.NewGuid().ToString();
        Company1NewSnapshotId = Guid.NewGuid().ToString();
        Company2SnapshotId    = Guid.NewGuid().ToString();
        Company3SnapshotId    = Guid.NewGuid().ToString();

        await InsertSnapshotAsync(connection, Company1OldSnapshotId, TestData.Itsm.ServiceNow, olderSnapshotDate);
        await InsertSnapshotAsync(connection, Company1NewSnapshotId, TestData.Itsm.ServiceNow, testSnapshotDate);
        await InsertSnapshotAsync(connection, Company2SnapshotId,    TestData.Itsm.Jira,        testSnapshotDate);
        await InsertSnapshotAsync(connection, Company3SnapshotId,    TestData.Itsm.ServiceNow,  testSnapshotDate);

        string[] company1TicketKeys = new[]
        {
            TestData.TicketKeys.Ticket1,
            TestData.TicketKeys.Ticket2,
            TestData.TicketKeys.Ticket3,
            TestData.TicketKeys.Ticket4,
            TestData.TicketKeys.Ticket5
        };

        foreach (string ticketKey in company1TicketKeys)
        {
            await InsertSnapshotTicketAsync(connection, Company1OldSnapshotId, company1Name, ticketKey);
            await InsertSnapshotTicketAsync(connection, Company1NewSnapshotId, company1Name, ticketKey);
        }

        string[] company2TicketKeys = new[] { "JIRA-001", "JIRA-002", "JIRA-003" };
        foreach (string ticketKey in company2TicketKeys)
        {
            await InsertSnapshotTicketAsync(connection, Company2SnapshotId, company2Name, ticketKey);
        }

        string[] company3TicketKeys = new[] { TestData.TicketKeys.Ticket1, "INC0002233", "INC0002234" };
        foreach (string ticketKey in company3TicketKeys)
        {
            await InsertSnapshotTicketAsync(connection, Company3SnapshotId, company3Name, ticketKey);
        }

        foreach (string ticketKey in company1TicketKeys)
        {
            await InsertFieldChangeAsync(connection, company1Name, ticketKey, TestData.Fields.Status,   TestData.FieldValues.StatusOpen,       olderSnapshotDate, Company1OldSnapshotId);
        }

        foreach (string ticketKey in company1TicketKeys)
        {
            await InsertFieldChangeAsync(connection, company1Name, ticketKey, TestData.Fields.Status,   TestData.FieldValues.StatusInProgress, testSnapshotDate,  Company1NewSnapshotId);
            await InsertFieldChangeAsync(connection, company1Name, ticketKey, TestData.Fields.Priority, TestData.FieldValues.PriorityHigh,      testSnapshotDate,  Company1NewSnapshotId);
            await InsertFieldChangeAsync(connection, company1Name, ticketKey, TestData.Fields.Assignee, "John Smith",                           testSnapshotDate,  Company1NewSnapshotId);
        }

        foreach (string ticketKey in company2TicketKeys)
        {
            await InsertFieldChangeAsync(connection, company2Name, ticketKey, TestData.Fields.Status,   TestData.FieldValues.StatusOpen,       testSnapshotDate, Company2SnapshotId);
            await InsertFieldChangeAsync(connection, company2Name, ticketKey, TestData.Fields.Priority, TestData.FieldValues.PriorityMedium,   testSnapshotDate, Company2SnapshotId);
        }

        foreach (string ticketKey in company3TicketKeys)
        {
            await InsertFieldChangeAsync(connection, company3Name, ticketKey, TestData.Fields.Status,     TestData.FieldValues.StatusResolved, testSnapshotDate, Company3SnapshotId);
            await InsertFieldChangeAsync(connection, company3Name, ticketKey, TestData.Fields.Resolution, "Fixed",                             testSnapshotDate, Company3SnapshotId);
        }

        await InsertFieldMappingAsync(connection, "servicenow", "incident_state",    "Status",   false);
        await InsertFieldMappingAsync(connection, "servicenow", "assigned_to",       "Assignee", false);
        await InsertFieldMappingAsync(connection, "servicenow", "short_description", "Summary",  false);
        await InsertFieldMappingAsync(connection, "jira",       "status",            "Status",   false);
        await InsertFieldMappingAsync(connection, "jira",       "assignee",          "Assignee", false);
        await InsertFieldMappingAsync(connection, "jira",       "summary",           "Summary",  false);
    }

    // ------------------------------------------------------------------
    // Helpers — use raw IDbCommand
    // ------------------------------------------------------------------

    private static async Task InsertSnapshotAsync(IDbConnection connection, string snapshotId, string itsmSource, DateTime snapshotDate)
    {
        using IDbCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO raw_snapshot (id, itsm_source, snapshot_date, uploaded_by, uploaded_at)
            VALUES (@Id, @ItsmSource, @SnapshotDate, @UploadedBy, @UploadedAt)
            """;
        AddParam(cmd, "Id",           snapshotId);
        AddParam(cmd, "ItsmSource",   itsmSource);
        AddParam(cmd, "SnapshotDate", snapshotDate);
        AddParam(cmd, "UploadedBy",   "test");
        AddParam(cmd, "UploadedAt",   DateTime.UtcNow);
        await ((System.Data.Common.DbCommand)cmd).ExecuteNonQueryAsync();
    }

    private static async Task InsertSnapshotTicketAsync(IDbConnection connection, string snapshotId, string companyName, string ticketKey)
    {
        using IDbCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO snapshot_ticket (id, snapshot_id, company_name, ticket_key)
            VALUES (@Id, @SnapshotId, @CompanyName, @TicketKey)
            """;
        AddParam(cmd, "Id",          Guid.NewGuid().ToString());
        AddParam(cmd, "SnapshotId",  snapshotId);
        AddParam(cmd, "CompanyName", companyName);
        AddParam(cmd, "TicketKey",   ticketKey);
        await ((System.Data.Common.DbCommand)cmd).ExecuteNonQueryAsync();
    }

    private static async Task InsertFieldMappingAsync(IDbConnection connection, string itsmSource, string sourceFieldName, string canonicalFieldName, bool isRequired)
    {
        using IDbCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name, is_required)
            VALUES (@ItsmSource, @SourceFieldName, @CanonicalFieldName, @IsRequired)
            ON CONFLICT (itsm_source, source_field_name) DO UPDATE
                SET canonical_field_name = excluded.canonical_field_name,
                    is_required          = excluded.is_required
            """;
        AddParam(cmd, "ItsmSource",          itsmSource);
        AddParam(cmd, "SourceFieldName",     sourceFieldName);
        AddParam(cmd, "CanonicalFieldName",  canonicalFieldName);
        AddParam(cmd, "IsRequired",          isRequired ? 1 : 0);
        await ((System.Data.Common.DbCommand)cmd).ExecuteNonQueryAsync();
    }

    private static async Task InsertFieldChangeAsync(IDbConnection connection, string companyName, string ticketKey, string fieldName, string? fieldValue, DateTime observedAt, string snapshotId)
    {
        using IDbCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO field_change (company_name, ticket_key, field_name, field_value, observed_at, snapshot_id)
            VALUES (@CompanyName, @TicketKey, @FieldName, @FieldValue, @ObservedAt, @SnapshotId)
            """;
        AddParam(cmd, "CompanyName", companyName);
        AddParam(cmd, "TicketKey",   ticketKey);
        AddParam(cmd, "FieldName",   fieldName);
        AddParam(cmd, "FieldValue",  (object?)fieldValue ?? DBNull.Value);
        AddParam(cmd, "ObservedAt",  observedAt);
        AddParam(cmd, "SnapshotId",  snapshotId);
        await ((System.Data.Common.DbCommand)cmd).ExecuteNonQueryAsync();
    }

    private static void AddParam(IDbCommand cmd, string name, object? value)
    {
        IDbDataParameter p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }
}
