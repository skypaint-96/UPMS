namespace UPMS.Web.Tests;

using UPMS.Web.Plugins;
using UPMS.Web.Plugins.Examples;

[TestFixture]
public class MonthEndLifecycleReportPluginTests
{
    [Test]
    [NonParallelizable]
    public async Task GenerateAsync_WithLifecycleFields_ReturnsHtmlTrendReport()
    {
        var (dataService, context, connection) = ReportPluginTestDataHelper.CreateDataService();
        using (connection)
        using (context)
        {
            var itsmSource = "servicenow";
            var company = "Acme";
            var snapshotDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
            var asOfDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);

            await ReportPluginTestDataHelper.SeedTicketAsync(
                dataService,
                itsmSource,
                company,
                "INC0001",
                snapshotDate,
                ("Number", "INC0001"),
                ("State", "Open"),
                ("Priority", "High"),
                ("Assigned To", "Alice"),
                ("Short Description", "Database issue"),
                ("Opened At", "2026-01-03"),
                ("Updated On", "2026-01-20"));

            await ReportPluginTestDataHelper.SeedTicketAsync(
                dataService,
                itsmSource,
                company,
                "INC0002",
                snapshotDate,
                ("Number", "INC0002"),
                ("State", "Closed"),
                ("Priority", "Low"),
                ("Assigned To", "Bob"),
                ("Short Description", "Printer issue"),
                ("Opened At", "2025-12-15"),
                ("Resolved At", "2026-01-10"),
                ("Updated On", "2026-01-10"));

            var plugin = new MonthEndLifecycleReportPlugin(dataService);
            var request = new ReportRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    ["itsm_source"] = itsmSource,
                    ["company"] = company,
                    ["as_of_date"] = asOfDate.ToString("yyyy-MM-dd"),
                    ["breakdown_field"] = "State",
                    ["detail_fields"] = "State,Priority,Assigned To,Short Description",
                    ["include_ticket_table"] = "true"
                }
            };

            var result = await plugin.GenerateAsync(request);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.OutputType, Is.EqualTo(ReportOutputType.HtmlContent));
            Assert.That(result.HtmlContent, Does.Contain("Month End Lifecycle Report"));
            Assert.That(result.HtmlContent, Does.Contain("Current Breakdown by State"));
            Assert.That(result.HtmlContent, Does.Contain("INC0001"));
            Assert.That(result.HtmlContent, Does.Contain("<svg"));
        }
    }
}
