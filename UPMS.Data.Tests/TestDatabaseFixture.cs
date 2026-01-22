namespace UPMS.Data.Tests;

using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Threading.Tasks;
using UPMS.Data;

public class TestDatabaseFixture
{
    private static readonly string TestDatabasePath = Path.Combine(
        Path.GetTempPath(),
        $"upms_test_db_{Guid.NewGuid()}.sqlite"
    );
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

        // No need to delete - using unique file name
        
        // Initialize TicketDataService with a function that creates new connections to the test database
        TicketDataService.Initialize(() => new SqliteConnection($"Data Source={TestDatabasePath}"));

        // Set up schema and test data
        using (SqliteConnection connection = new SqliteConnection($"Data Source={TestDatabasePath}"))
        {
            await connection.OpenAsync();
            await CreateTablesAsync(connection);
            await SeedTestDataAsync(connection);
        }

        _initialized = true;
    }

    public static async Task CleanupAsync()
    {
        if (File.Exists(TestDatabasePath))
        {
            try
            {
                File.Delete(TestDatabasePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        _initialized = false;
    }

    private static async Task CreateTablesAsync(SqliteConnection connection)
    {
        // SQLite doesn't have UUID type, so we use TEXT instead
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

            CREATE INDEX IF NOT EXISTS idx_snapshot_ticket_snapshot_id ON snapshot_ticket (snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_ticket_company_name ON snapshot_ticket (company_name);
            CREATE INDEX IF NOT EXISTS idx_field_change_company_ticket_time ON field_change (company_name, ticket_key, observed_at DESC);
            CREATE INDEX IF NOT EXISTS idx_raw_snapshot_itsm_source ON raw_snapshot (itsm_source);
            """;

        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = createTablesSQL;
        int unused = await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SeedTestDataAsync(SqliteConnection connection)
    {
        string company1Name = TestData.Companies.Company1;
        string company2Name = TestData.Companies.Company2;
        string company3Name = TestData.Companies.Company3;

        DateTime testSnapshotDate = TestData.Dates.NewSnapshotDate;
        DateTime olderSnapshotDate = TestData.Dates.OlderSnapshotDate;

        Company1OldSnapshotId = Guid.NewGuid().ToString();
        Company1NewSnapshotId = Guid.NewGuid().ToString();
        Company2SnapshotId = Guid.NewGuid().ToString();
        Company3SnapshotId = Guid.NewGuid().ToString();

        // Insert old snapshot for ServiceNow source
        await InsertSnapshotAsync(connection, Company1OldSnapshotId, TestData.Itsm.ServiceNow, olderSnapshotDate);

        // Insert new snapshot for ServiceNow source
        await InsertSnapshotAsync(connection, Company1NewSnapshotId, TestData.Itsm.ServiceNow, testSnapshotDate);

        // Insert snapshot for Jira source
        await InsertSnapshotAsync(connection, Company2SnapshotId, TestData.Itsm.Jira, testSnapshotDate);

        // Insert snapshot for ServiceNow source (company 3)
        await InsertSnapshotAsync(connection, Company3SnapshotId, TestData.Itsm.ServiceNow, testSnapshotDate);

        // Insert snapshot tickets for company 1 in both old and new ServiceNow snapshots
        string[] company1TicketKeys = new[] { 
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

        // Insert snapshot tickets for company 2 in Jira snapshot
        string[] company2TicketKeys = new[] { "JIRA-001", "JIRA-002", "JIRA-003" };
        foreach (string ticketKey in company2TicketKeys)
        {
            await InsertSnapshotTicketAsync(connection, Company2SnapshotId, company2Name, ticketKey);
        }

        // Insert snapshot tickets for company 3 in ServiceNow snapshot
        string[] company3TicketKeys = new[] { TestData.TicketKeys.Ticket1, "INC0002233", "INC0002234" };
        foreach (string ticketKey in company3TicketKeys)
        {
            await InsertSnapshotTicketAsync(connection, Company3SnapshotId, company3Name, ticketKey);
        }

        // Insert field changes for company 1 tickets from old snapshot
        foreach (string ticketKey in company1TicketKeys)
        {
            await InsertFieldChangeAsync(connection, company1Name, ticketKey, TestData.Fields.Status, TestData.FieldValues.StatusOpen, olderSnapshotDate, Company1OldSnapshotId);
        }

        // Insert field changes for company 1 tickets from new snapshot
        foreach (string ticketKey in company1TicketKeys)
        {
            await InsertFieldChangeAsync(connection, company1Name, ticketKey, TestData.Fields.Status, TestData.FieldValues.StatusInProgress, testSnapshotDate, Company1NewSnapshotId);
            await InsertFieldChangeAsync(connection, company1Name, ticketKey, TestData.Fields.Priority, TestData.FieldValues.PriorityHigh, testSnapshotDate, Company1NewSnapshotId);
            await InsertFieldChangeAsync(connection, company1Name, ticketKey, TestData.Fields.Assignee, "John Smith", testSnapshotDate, Company1NewSnapshotId);
        }

        // Insert field changes for company 2 tickets
        foreach (string ticketKey in company2TicketKeys)
        {
            await InsertFieldChangeAsync(connection, company2Name, ticketKey, TestData.Fields.Status, TestData.FieldValues.StatusOpen, testSnapshotDate, Company2SnapshotId);
            await InsertFieldChangeAsync(connection, company2Name, ticketKey, TestData.Fields.Priority, TestData.FieldValues.PriorityMedium, testSnapshotDate, Company2SnapshotId);
        }

        // Insert field changes for company 3 tickets
        foreach (string ticketKey in company3TicketKeys)
        {
            await InsertFieldChangeAsync(connection, company3Name, ticketKey, TestData.Fields.Status, TestData.FieldValues.StatusResolved, testSnapshotDate, Company3SnapshotId);
            await InsertFieldChangeAsync(connection, company3Name, ticketKey, TestData.Fields.Resolution, "Fixed", testSnapshotDate, Company3SnapshotId);
        }
    }

    private static async Task InsertSnapshotAsync(SqliteConnection connection, string snapshotId, string itsmSource, DateTime snapshotDate)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO raw_snapshot (id, itsm_source, snapshot_date, uploaded_by, uploaded_at)
            VALUES (@Id, @ItsmSource, @SnapshotDate, @UploadedBy, @UploadedAt)
            """;
        cmd.Parameters.AddWithValue("Id", snapshotId);
        cmd.Parameters.AddWithValue("ItsmSource", itsmSource);
        cmd.Parameters.AddWithValue("SnapshotDate", snapshotDate);
        cmd.Parameters.AddWithValue("UploadedBy", "test");
        cmd.Parameters.AddWithValue("UploadedAt", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertSnapshotTicketAsync(SqliteConnection connection, string snapshotId, string companyName, string ticketKey)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO snapshot_ticket (id, snapshot_id, company_name, ticket_key)
            VALUES (@Id, @SnapshotId, @CompanyName, @TicketKey)
            """;
        cmd.Parameters.AddWithValue("Id", Guid.NewGuid().ToString());
        cmd.Parameters.AddWithValue("SnapshotId", snapshotId);
        cmd.Parameters.AddWithValue("CompanyName", companyName);
        cmd.Parameters.AddWithValue("TicketKey", ticketKey);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertFieldChangeAsync(SqliteConnection connection, string companyName, string ticketKey, string fieldName, string? fieldValue, DateTime observedAt, string snapshotId)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO field_change (company_name, ticket_key, field_name, field_value, observed_at, snapshot_id)
            VALUES (@CompanyName, @TicketKey, @FieldName, @FieldValue, @ObservedAt, @SnapshotId)
            """;
        cmd.Parameters.AddWithValue("CompanyName", companyName);
        cmd.Parameters.AddWithValue("TicketKey", ticketKey);
        cmd.Parameters.AddWithValue("FieldName", fieldName);
        cmd.Parameters.AddWithValue("FieldValue", fieldValue ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("ObservedAt", observedAt);
        cmd.Parameters.AddWithValue("SnapshotId", snapshotId);
        await cmd.ExecuteNonQueryAsync();
    }
}
