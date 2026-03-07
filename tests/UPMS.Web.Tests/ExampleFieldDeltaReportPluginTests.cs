namespace UPMS.Web.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using UPMS.Data;
using UPMS.Web.Plugins;
using UPMS.Web.Plugins.Examples;

[TestFixture]
public class ExampleFieldDeltaReportPluginTests
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
    public async Task GenerateAsync_WithTwoSnapshots_ShowsTransitionsAndCounts()
    {
        // Arrange
        var itsmSource = "servicenow";
        var company = "Acme";

        var snapshot1Date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var snapshot2Date = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var snapshot1Id = await _dataService.CreateSnapshotAsync(itsmSource, snapshot1Date, uploadedBy: "tester");
        var snapshot2Id = await _dataService.CreateSnapshotAsync(itsmSource, snapshot2Date, uploadedBy: "tester");

        var ticket1 = TicketKeyFactory.Compose(itsmSource, company, "INC0001");
        var ticket2 = TicketKeyFactory.Compose(itsmSource, company, "INC0002");

        // Snapshot 1: ticket1 is open
        await _dataService.AddTicketsToSnapshotAsync(snapshot1Id, new[] { (ticket1, company) });
        await _dataService.RecordFieldChangeAsync(company, ticket1, "status", "open", snapshot1Date, snapshot1Id);

        // Snapshot 2: ticket1 becomes closed, ticket2 is new and open
        await _dataService.AddTicketsToSnapshotAsync(snapshot2Id, new[] { (ticket1, company), (ticket2, company) });
        await _dataService.RecordFieldChangeAsync(company, ticket1, "status", "closed", snapshot2Date, snapshot2Id);
        await _dataService.RecordFieldChangeAsync(company, ticket2, "status", "open", snapshot2Date, snapshot2Id);

        var plugin = new FieldDeltaReportPlugin(_dataService);
        var request = new ReportRequest
        {
            Parameters = new Dictionary<string, string>
            {
                ["itsm_source"] = itsmSource,
                ["company"] = company,
                ["date_range_from"] = "2026-01-15",
                ["date_range_to"] = "2026-02-15",
                ["field_name"] = "status",
                ["include_ticket_list"] = "true"
            }
        };

        // Act
        var result = await plugin.GenerateAsync(request);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.OutputType, Is.EqualTo(ReportOutputType.HtmlContent));
        Assert.That(result.HtmlContent, Does.Contain("Field Delta"));
        Assert.That(result.HtmlContent, Does.Contain("New tickets"));
        Assert.That(result.HtmlContent, Does.Contain(">open<"));
        Assert.That(result.HtmlContent, Does.Contain(">closed<"));
        Assert.That(result.HtmlContent, Does.Contain(ticket1));
    }
}
