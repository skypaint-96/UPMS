namespace UPMS.Web.Tests;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using UPMS.Data;
using UPMS.Web.Plugins;
using UPMS.Web.Plugins.Examples;

[TestFixture]
public class ExampleTicketCsvExportPluginTests
{
    private SqliteConnection _connection = null!;
    private UpmsDbContext _context = null!;
    private TicketDataServiceInstance _dataService = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var cmd = _connection.CreateCommand();
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
            .UseSqlite(_connection)
            .Options;

        _context = new UpmsDbContext(options);
        _dataService = new TicketDataServiceInstance(new TicketDataService(_context));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task GenerateAsync_ReturnsCsvFileDownload()
    {
        // Arrange
        var itsmSource = "servicenow";
        var company = "Acme";
        var snapshotDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var snapshotId = await _dataService.CreateSnapshotAsync(itsmSource, snapshotDate, uploadedBy: "tester");

        var ticket1 = TicketKeyFactory.Compose(itsmSource, company, "INC0001");
        await _dataService.AddTicketsToSnapshotAsync(snapshotId, new[] { (ticket1, company) });
        await _dataService.RecordFieldChangeAsync(company, ticket1, "status", "open", snapshotDate, snapshotId);
        await _dataService.RecordFieldChangeAsync(company, ticket1, "priority", "P1", snapshotDate, snapshotId);

        var plugin = new TicketCsvExportReportPlugin(_dataService);
        var request = new ReportRequest
        {
            Parameters = new Dictionary<string, string>
            {
                ["itsm_source"] = itsmSource,
                ["company"] = company,
                ["as_of_date"] = "2026-01-15",
                ["fields"] = "status,priority"
            }
        };

        // Act
        var result = await plugin.GenerateAsync(request);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.OutputType, Is.EqualTo(ReportOutputType.FileDownload));
        Assert.That(result.ContentType, Is.EqualTo("text/csv"));
        Assert.That(result.FileName, Does.EndWith(".csv"));
        Assert.That(result.FileContent, Is.Not.Null);

        var csv = Encoding.UTF8.GetString(result.FileContent!);
        Assert.That(csv, Does.Contain("ticket_key"));
        Assert.That(csv, Does.Contain("ticket_number"));
        Assert.That(csv, Does.Contain("status"));
        Assert.That(csv, Does.Contain("priority"));
        Assert.That(csv, Does.Contain(ticket1));
        Assert.That(csv, Does.Contain("open"));
        Assert.That(csv, Does.Contain("P1"));
    }
}
