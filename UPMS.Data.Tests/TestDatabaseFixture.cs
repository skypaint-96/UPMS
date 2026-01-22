namespace UPMS.Data.Tests;

using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using UPMS.Data;

public class TestDatabaseFixture
{
    private static readonly string TestDatabasePath = Path.Combine(
        Path.GetTempPath(), 
        "upms_test_db.sqlite"
    );
    private static bool _initialized = false;

    public static async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        // Clean up old database if it exists
        if (File.Exists(TestDatabasePath))
        {
            File.Delete(TestDatabasePath);
        }

        // Initialize DBIO_Ticket with a function that creates new connections to the test database
        DBIO_Ticket.Initialize(() => new SqliteConnection($"Data Source={TestDatabasePath}"));

        // Set up schema and test data
        using (var connection = new SqliteConnection($"Data Source={TestDatabasePath}"))
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
                company_id TEXT NOT NULL,
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
                company_id TEXT NOT NULL,
                ticket_key VARCHAR(255) NOT NULL,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT uq_snapshot_ticket UNIQUE (snapshot_id, ticket_key)
            );

            CREATE TABLE IF NOT EXISTS field_change (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                company_id TEXT NOT NULL,
                ticket_key VARCHAR(255) NOT NULL,
                field_name VARCHAR(255) NOT NULL,
                field_value TEXT,
                observed_at DATETIME NOT NULL,
                snapshot_id TEXT NOT NULL REFERENCES raw_snapshot(id) ON DELETE CASCADE,
                created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
            );

            CREATE INDEX IF NOT EXISTS idx_raw_snapshot_company_id ON raw_snapshot (company_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_ticket_snapshot_id ON snapshot_ticket (snapshot_id);
            CREATE INDEX IF NOT EXISTS idx_snapshot_ticket_company_id ON snapshot_ticket (company_id);
            CREATE INDEX IF NOT EXISTS idx_field_change_company_ticket_time ON field_change (company_id, ticket_key, observed_at DESC);
            """;

        using var cmd = connection.CreateCommand();
        cmd.CommandText = createTablesSQL;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task SeedTestDataAsync(SqliteConnection connection)
    {
        var testCompanyId = "00000001-0000-0000-0000-000000000001";
        var company2Id = "00000002-0000-0000-0000-000000000002";
        var testSnapshotDate = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var olderSnapshotDate = new DateTime(2025, 1, 10, 12, 0, 0, DateTimeKind.Utc);

        var oldSnapshotId = Guid.NewGuid().ToString();
        var newSnapshotId = Guid.NewGuid().ToString();
        var company2SnapshotId = Guid.NewGuid().ToString();

        // Insert old snapshot for company 1
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO raw_snapshot (id, company_id, itsm_source, snapshot_date, uploaded_by, uploaded_at)
                VALUES (@Id, @CompanyId, @ItsmSource, @SnapshotDate, @UploadedBy, @UploadedAt)
                """;
            cmd.Parameters.AddWithValue("Id", oldSnapshotId);
            cmd.Parameters.AddWithValue("CompanyId", testCompanyId);
            cmd.Parameters.AddWithValue("ItsmSource", "servicenow");
            cmd.Parameters.AddWithValue("SnapshotDate", olderSnapshotDate);
            cmd.Parameters.AddWithValue("UploadedBy", "test");
            cmd.Parameters.AddWithValue("UploadedAt", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert new snapshot for company 1
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO raw_snapshot (id, company_id, itsm_source, snapshot_date, uploaded_by, uploaded_at)
                VALUES (@Id, @CompanyId, @ItsmSource, @SnapshotDate, @UploadedBy, @UploadedAt)
                """;
            cmd.Parameters.AddWithValue("Id", newSnapshotId);
            cmd.Parameters.AddWithValue("CompanyId", testCompanyId);
            cmd.Parameters.AddWithValue("ItsmSource", "servicenow");
            cmd.Parameters.AddWithValue("SnapshotDate", testSnapshotDate);
            cmd.Parameters.AddWithValue("UploadedBy", "test");
            cmd.Parameters.AddWithValue("UploadedAt", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }

        // Insert snapshot for company 2
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO raw_snapshot (id, company_id, itsm_source, snapshot_date, uploaded_by, uploaded_at)
                VALUES (@Id, @CompanyId, @ItsmSource, @SnapshotDate, @UploadedBy, @UploadedAt)
                """;
            cmd.Parameters.AddWithValue("Id", company2SnapshotId);
            cmd.Parameters.AddWithValue("CompanyId", company2Id);
            cmd.Parameters.AddWithValue("ItsmSource", "jira");
            cmd.Parameters.AddWithValue("SnapshotDate", testSnapshotDate);
            cmd.Parameters.AddWithValue("UploadedBy", "test");
            cmd.Parameters.AddWithValue("UploadedAt", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }

        // Test tickets for company 1
        var ticketKeys = new[] { "INC0001234", "INC0001235", "INC0001236", "INC0001237", "INC0001238" };

        // Insert snapshot tickets for company 1 in BOTH old and new snapshots
        foreach (var ticketKey in ticketKeys)
        {
            // Old snapshot
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO snapshot_ticket (id, snapshot_id, company_id, ticket_key)
                    VALUES (@Id, @SnapshotId, @CompanyId, @TicketKey)
                    """;
                cmd.Parameters.AddWithValue("Id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("SnapshotId", oldSnapshotId);
                cmd.Parameters.AddWithValue("CompanyId", testCompanyId);
                cmd.Parameters.AddWithValue("TicketKey", ticketKey);
                await cmd.ExecuteNonQueryAsync();
            }

            // New snapshot
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO snapshot_ticket (id, snapshot_id, company_id, ticket_key)
                    VALUES (@Id, @SnapshotId, @CompanyId, @TicketKey)
                    """;
                cmd.Parameters.AddWithValue("Id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("SnapshotId", newSnapshotId);
                cmd.Parameters.AddWithValue("CompanyId", testCompanyId);
                cmd.Parameters.AddWithValue("TicketKey", ticketKey);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // Test tickets for company 2
        var company2TicketKeys = new[] { "JIRA-001", "JIRA-002", "JIRA-003" };

        foreach (var ticketKey in company2TicketKeys)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO snapshot_ticket (id, snapshot_id, company_id, ticket_key)
                    VALUES (@Id, @SnapshotId, @CompanyId, @TicketKey)
                    """;
                cmd.Parameters.AddWithValue("Id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("SnapshotId", company2SnapshotId);
                cmd.Parameters.AddWithValue("CompanyId", company2Id);
                cmd.Parameters.AddWithValue("TicketKey", ticketKey);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // Insert field changes for company 1 tickets from old snapshot
        foreach (var ticketKey in ticketKeys)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO field_change (company_id, ticket_key, field_name, field_value, observed_at, snapshot_id)
                    VALUES (@CompanyId, @TicketKey, @FieldName, @FieldValue, @ObservedAt, @SnapshotId)
                    """;
                cmd.Parameters.AddWithValue("CompanyId", testCompanyId);
                cmd.Parameters.AddWithValue("TicketKey", ticketKey);
                cmd.Parameters.AddWithValue("FieldName", "Status");
                cmd.Parameters.AddWithValue("FieldValue", "Open");
                cmd.Parameters.AddWithValue("ObservedAt", olderSnapshotDate);
                cmd.Parameters.AddWithValue("SnapshotId", oldSnapshotId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // Insert field changes for company 1 tickets from new snapshot
        foreach (var ticketKey in ticketKeys)
        {
            // New observation
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO field_change (company_id, ticket_key, field_name, field_value, observed_at, snapshot_id)
                    VALUES (@CompanyId, @TicketKey, @FieldName, @FieldValue, @ObservedAt, @SnapshotId)
                    """;
                cmd.Parameters.AddWithValue("CompanyId", testCompanyId);
                cmd.Parameters.AddWithValue("TicketKey", ticketKey);
                cmd.Parameters.AddWithValue("FieldName", "Status");
                cmd.Parameters.AddWithValue("FieldValue", "In Progress");
                cmd.Parameters.AddWithValue("ObservedAt", testSnapshotDate);
                cmd.Parameters.AddWithValue("SnapshotId", newSnapshotId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Priority field
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO field_change (company_id, ticket_key, field_name, field_value, observed_at, snapshot_id)
                    VALUES (@CompanyId, @TicketKey, @FieldName, @FieldValue, @ObservedAt, @SnapshotId)
                    """;
                cmd.Parameters.AddWithValue("CompanyId", testCompanyId);
                cmd.Parameters.AddWithValue("TicketKey", ticketKey);
                cmd.Parameters.AddWithValue("FieldName", "Priority");
                cmd.Parameters.AddWithValue("FieldValue", "High");
                cmd.Parameters.AddWithValue("ObservedAt", testSnapshotDate);
                cmd.Parameters.AddWithValue("SnapshotId", newSnapshotId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Assignee field (nullable)
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO field_change (company_id, ticket_key, field_name, field_value, observed_at, snapshot_id)
                    VALUES (@CompanyId, @TicketKey, @FieldName, @FieldValue, @ObservedAt, @SnapshotId)
                    """;
                cmd.Parameters.AddWithValue("CompanyId", testCompanyId);
                cmd.Parameters.AddWithValue("TicketKey", ticketKey);
                cmd.Parameters.AddWithValue("FieldName", "Assignee");
                cmd.Parameters.AddWithValue("FieldValue", DBNull.Value);
                cmd.Parameters.AddWithValue("ObservedAt", testSnapshotDate);
                cmd.Parameters.AddWithValue("SnapshotId", newSnapshotId);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // Insert field changes for company 2 tickets
        foreach (var ticketKey in company2TicketKeys)
        {
            // Status field
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO field_change (company_id, ticket_key, field_name, field_value, observed_at, snapshot_id)
                    VALUES (@CompanyId, @TicketKey, @FieldName, @FieldValue, @ObservedAt, @SnapshotId)
                    """;
                cmd.Parameters.AddWithValue("CompanyId", company2Id);
                cmd.Parameters.AddWithValue("TicketKey", ticketKey);
                cmd.Parameters.AddWithValue("FieldName", "Status");
                cmd.Parameters.AddWithValue("FieldValue", "Done");
                cmd.Parameters.AddWithValue("ObservedAt", testSnapshotDate);
                cmd.Parameters.AddWithValue("SnapshotId", company2SnapshotId);
                await cmd.ExecuteNonQueryAsync();
            }

            // Priority field
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO field_change (company_id, ticket_key, field_name, field_value, observed_at, snapshot_id)
                    VALUES (@CompanyId, @TicketKey, @FieldName, @FieldValue, @ObservedAt, @SnapshotId)
                    """;
                cmd.Parameters.AddWithValue("CompanyId", company2Id);
                cmd.Parameters.AddWithValue("TicketKey", ticketKey);
                cmd.Parameters.AddWithValue("FieldName", "Priority");
                cmd.Parameters.AddWithValue("FieldValue", "Medium");
                cmd.Parameters.AddWithValue("ObservedAt", testSnapshotDate);
                cmd.Parameters.AddWithValue("SnapshotId", company2SnapshotId);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
