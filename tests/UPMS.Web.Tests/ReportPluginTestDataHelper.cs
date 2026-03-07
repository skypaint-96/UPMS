namespace UPMS.Web.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UPMS.Data;

internal static class ReportPluginTestDataHelper
{
    public static (TicketDataServiceInstance DataService, UpmsDbContext Context, SqliteConnection Connection) CreateDataService()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE raw_snapshot (
    id TEXT PRIMARY KEY,
    itsm_source TEXT NOT NULL,
    snapshot_date TEXT NOT NULL,
    uploaded_by TEXT NOT NULL,
    uploaded_at TEXT NOT NULL,
    upload_metadata TEXT
);
CREATE TABLE snapshot_ticket (
    id TEXT PRIMARY KEY,
    snapshot_id TEXT NOT NULL,
    company_name TEXT NOT NULL,
    ticket_key TEXT NOT NULL
);
CREATE TABLE field_change (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    company_name TEXT NOT NULL,
    ticket_key TEXT NOT NULL,
    field_name TEXT NOT NULL,
    field_value TEXT,
    observed_at TEXT NOT NULL,
    snapshot_id TEXT NOT NULL
);
CREATE TABLE itsm_source (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    display_label TEXT NOT NULL
);
CREATE TABLE itsm_field_mapping (
    itsm_source TEXT NOT NULL,
    source_field_name TEXT NOT NULL,
    canonical_field_name TEXT NOT NULL,
    is_required INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (itsm_source, source_field_name)
);
";
        cmd.ExecuteNonQuery();

        var options = new DbContextOptionsBuilder<UpmsDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new UpmsDbContext(options);
        var service = new TicketDataServiceInstance(new TicketDataService(context));
        return (service, context, connection);
    }

    public static async Task<string> SeedTicketAsync(
        TicketDataServiceInstance dataService,
        string itsmSource,
        string company,
        string number,
        DateTime snapshotDate,
        params (string FieldName, string? Value)[] fields)
    {
        var snapshotId = await dataService.CreateSnapshotAsync(itsmSource, snapshotDate, uploadedBy: "tester");
        var ticketKey = TicketKeyFactory.Compose(itsmSource, company, number);

        await dataService.AddTicketsToSnapshotAsync(snapshotId, [(ticketKey, company)]);
        foreach (var field in fields)
        {
            await dataService.RecordFieldChangeAsync(company, ticketKey, field.FieldName, field.Value, snapshotDate, snapshotId);
        }

        return ticketKey;
    }
}
