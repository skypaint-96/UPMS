namespace UPMS.Data.Tests;

using Microsoft.Data.Sqlite;
using Npgsql;
using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using UPMS.Data;

public class TestDatabaseFixture
{
    // Default fallback for local SQLite-based tests (no env var set)
    private static readonly string SqliteTestDatabasePath = Path.Combine(
        Path.GetTempPath(),
        $"upms_test_db_{Guid.NewGuid()}.sqlite"
    );

    // Connection string resolved once at startup
    private static readonly string ResolvedConnectionString =
        Environment.GetEnvironmentVariable("UPMS_TEST_CONNECTION_STRING")
        ?? string.Empty; // empty → use SQLite

    private static readonly bool UsePostgres = !string.IsNullOrWhiteSpace(ResolvedConnectionString);

    private static readonly string DefaultTestConnectionString =
        UsePostgres
            ? ResolvedConnectionString
            : $"Data Source={SqliteTestDatabasePath}";

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

        if (UsePostgres)
        {
            TicketDataService.Initialize(() => new NpgsqlConnection(DefaultTestConnectionString));

            using IDbConnection connection = new NpgsqlConnection(DefaultTestConnectionString);
            connection.Open();
            await CreatePostgresTablesAsync(connection);
            await SeedTestDataAsync(connection);
        }
        else
        {
            TicketDataService.Initialize(() => new SqliteConnection(DefaultTestConnectionString));

            using SqliteConnection connection = new SqliteConnection(DefaultTestConnectionString);
            await connection.OpenAsync();
            await CreateSqliteTablesAsync(connection);
            await SeedTestDataAsync(connection);
        }

        _initialized = true;
    }

    public static async Task CleanupAsync()
    {
        if (!UsePostgres && File.Exists(SqliteTestDatabasePath))
        {
            try
            {
                File.Delete(SqliteTestDatabasePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _initialized = false;
    }

    public static IItsmFieldMappingService GetMappingService()
    {
        if (UsePostgres)
        {
            return new ItsmFieldMappingService(() => new NpgsqlConnection(DefaultTestConnectionString));
        }
        return new ItsmFieldMappingService(() => new SqliteConnection(DefaultTestConnectionString));
    }

    public static IItsmSourceService GetSourceService()
    {
        if (UsePostgres)
        {
            return new ItsmSourceService(() => new NpgsqlConnection(DefaultTestConnectionString));
        }
        return new ItsmSourceService(() => new SqliteConnection(DefaultTestConnectionString));
    }

    // ------------------------------------------------------------------
    // PostgreSQL schema creation
    // ------------------------------------------------------------------
    private static async Task CreatePostgresTablesAsync(IDbConnection connection)
    {
        const string sql = """
            CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

            CREATE TABLE IF NOT EXISTS raw_snapshot (
                id              TEXT PRIMARY KEY,
                itsm_source     VARCHAR(100) NOT NULL,
                snapshot_date   TIMESTAMPTZ NOT NULL,
                uploaded_by     VARCHAR(255) NOT NULL,
                uploaded_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                upload_metadata TEXT,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS snapshot_ticket (
                id              TEXT PRIMARY KEY,
                snapshot_id     TEXT NOT NULL REFERENCES raw_snapshot(id) ON DELETE CASCADE,
                company_name    VARCHAR(255) NOT NULL,
                ticket_key      VARCHAR(255) NOT NULL,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                CONSTRAINT uq_snapshot_ticket UNIQUE (snapshot_id, ticket_key)
            );

            CREATE TABLE IF NOT EXISTS field_change (
                id              BIGSERIAL PRIMARY KEY,
                company_name    VARCHAR(255) NOT NULL,
                ticket_key      VARCHAR(255) NOT NULL,
                field_name      VARCHAR(255) NOT NULL,
                field_value     TEXT,
                observed_at     TIMESTAMPTZ NOT NULL,
                snapshot_id     TEXT NOT NULL REFERENCES raw_snapshot(id) ON DELETE CASCADE,
                created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS itsm_source (
                id            SERIAL PRIMARY KEY,
                name          TEXT NOT NULL,
                display_label TEXT NOT NULL,
                created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                CONSTRAINT uq_itsm_source_name UNIQUE (name)
            );

            CREATE TABLE IF NOT EXISTS itsm_field_mapping (
                itsm_source             VARCHAR(100)  NOT NULL,
                source_field_name       VARCHAR(255)  NOT NULL,
                canonical_field_name    VARCHAR(255)  NOT NULL,
                is_required             BOOLEAN       NOT NULL DEFAULT FALSE,
                PRIMARY KEY (itsm_source, source_field_name)
            );

            CREATE INDEX IF NOT EXISTS idx_snapshot_ticket_snapshot_id  ON snapshot_ticket (snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_ticket_company_name  ON snapshot_ticket (company_name);
            CREATE INDEX IF NOT EXISTS idx_field_change_company_ticket   ON field_change (company_name, ticket_key, observed_at DESC);
            CREATE INDEX IF NOT EXISTS idx_raw_snapshot_itsm_source      ON raw_snapshot (itsm_source);
            CREATE INDEX IF NOT EXISTS idx_itsm_field_mapping_source     ON itsm_field_mapping (itsm_source);
            """;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await ((System.Data.Common.DbCommand)cmd).ExecuteNonQueryAsync();
    }

    // ------------------------------------------------------------------
    // SQLite schema creation
    // ------------------------------------------------------------------
    private static async Task CreateSqliteTablesAsync(SqliteConnection connection)
    {
        const string createTablesSQL = """
            CREATE TABLE IF NOT EXISTS raw_snapshot (
                id TEXT PRIMARY KEY,
                itsm_source VARCHAR(100) NOT NULL,
                snapshot_date DATETIME NOT NULL,
                uploaded_by VARCHAR(255) NOT NULL,
                uploaded_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                upload_metadata TEXT,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS snapshot_ticket (
                id TEXT PRIMARY KEY,
                snapshot_id TEXT NOT NULL REFERENCES raw_snapshot(id) ON DELETE CASCADE,
                company_name VARCHAR(255) NOT NULL,
                ticket_key VARCHAR(255) NOT NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT uq_snapshot_ticket UNIQUE (snapshot_id, ticket_key)
            );

            CREATE TABLE IF NOT EXISTS field_change (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                company_name VARCHAR(255) NOT NULL,
                ticket_key VARCHAR(255) NOT NULL,
                field_name VARCHAR(255) NOT NULL,
                field_value TEXT,
                observed_at DATETIME NOT NULL,
                snapshot_id TEXT NOT NULL REFERENCES raw_snapshot(id) ON DELETE CASCADE,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS itsm_source (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                name          TEXT NOT NULL,
                display_label TEXT NOT NULL,
                created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT uq_itsm_source_name UNIQUE (name)
            );

            CREATE TABLE IF NOT EXISTS itsm_field_mapping (
                itsm_source VARCHAR(100) NOT NULL,
                source_field_name VARCHAR(255) NOT NULL,
                canonical_field_name VARCHAR(255) NOT NULL,
                is_required INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (itsm_source, source_field_name)
            );

            CREATE INDEX IF NOT EXISTS idx_snapshot_ticket_snapshot_id ON snapshot_ticket (snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_ticket_company_name ON snapshot_ticket (company_name);
            CREATE INDEX IF NOT EXISTS idx_field_change_company_ticket_time ON field_change (company_name, ticket_key, observed_at DESC);
            CREATE INDEX IF NOT EXISTS idx_raw_snapshot_itsm_source ON raw_snapshot (itsm_source);
            CREATE INDEX IF NOT EXISTS idx_itsm_field_mapping_source ON itsm_field_mapping (itsm_source);
            """;

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = createTablesSQL;
        int unused = await cmd.ExecuteNonQueryAsync();
    }

    // ------------------------------------------------------------------
    // Shared seed logic — works for both backends via IDbConnection
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

        string[] company1TicketKeys =
        [
            TestData.TicketKeys.Ticket1,
            TestData.TicketKeys.Ticket2,
            TestData.TicketKeys.Ticket3,
            TestData.TicketKeys.Ticket4,
            TestData.TicketKeys.Ticket5
        ];

        foreach (string ticketKey in company1TicketKeys)
        {
            await InsertSnapshotTicketAsync(connection, Company1OldSnapshotId, company1Name, ticketKey);
            await InsertSnapshotTicketAsync(connection, Company1NewSnapshotId, company1Name, ticketKey);
        }

        string[] company2TicketKeys = ["JIRA-001", "JIRA-002", "JIRA-003"];
        foreach (string ticketKey in company2TicketKeys)
        {
            await InsertSnapshotTicketAsync(connection, Company2SnapshotId, company2Name, ticketKey);
        }

        string[] company3TicketKeys = [TestData.TicketKeys.Ticket1, "INC0002233", "INC0002234"];
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
    // Helpers — use raw IDbCommand so they work for both SQLite and Postgres
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
        if (UsePostgres)
        {
            cmd.CommandText = """
                INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name, is_required)
                VALUES (@ItsmSource, @SourceFieldName, @CanonicalFieldName, @IsRequired)
                ON CONFLICT (itsm_source, source_field_name) DO UPDATE
                    SET canonical_field_name = EXCLUDED.canonical_field_name,
                        is_required          = EXCLUDED.is_required
                """;
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name, is_required)
                VALUES (@ItsmSource, @SourceFieldName, @CanonicalFieldName, @IsRequired)
                ON CONFLICT (itsm_source, source_field_name) DO UPDATE
                    SET canonical_field_name = excluded.canonical_field_name,
                        is_required          = excluded.is_required
                """;
        }
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
