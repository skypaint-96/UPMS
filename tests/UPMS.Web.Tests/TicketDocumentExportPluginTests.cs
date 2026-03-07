namespace UPMS.Web.Tests;

using System.Text;
using UPMS.Web.Plugins;
using UPMS.Web.Plugins.Documents;

[TestFixture]
public class TicketDocumentExportPluginTests
{
    [Test]
    [NonParallelizable]
    public async Task GenerateAsync_DocxFormat_ReturnsWordDocument()
    {
        var (dataService, context, connection) = ReportPluginTestDataHelper.CreateDataService();
        using (connection)
        using (context)
        {
            var itsmSource = "servicenow";
            var company = "Acme";
            var snapshotDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
            await ReportPluginTestDataHelper.SeedTicketAsync(
                dataService,
                itsmSource,
                company,
                "INC1001",
                snapshotDate,
                ("Number", "INC1001"),
                ("State", "Open"),
                ("Priority", "High"),
                ("Assigned To", "Alice"),
                ("Opened At", "2026-01-10"),
                ("Short Description", "Server outage"));

            var plugin = new TicketDocumentExportPlugin(dataService);
            var request = new ReportRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    ["itsm_source"] = itsmSource,
                    ["company"] = company,
                    ["as_of_date"] = snapshotDate.ToString("yyyy-MM-dd"),
                    ["output_format"] = TicketDocumentExportPlugin.FormatDocx,
                    ["ticket_keys"] = "INC1001",
                    ["fields"] = "Number,State,Priority,Assigned To,Short Description"
                }
            };

            var result = await plugin.GenerateAsync(request);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.OutputType, Is.EqualTo(ReportOutputType.FileDownload));
            Assert.That(result.ContentType, Does.Contain("wordprocessingml.document"));
            Assert.That(result.FileName, Does.EndWith(".docx"));
            Assert.That(result.FileContent, Is.Not.Null);
            Assert.That(Encoding.ASCII.GetString(result.FileContent!.Take(2).ToArray()), Is.EqualTo("PK"));
        }
    }

    [Test]
    [NonParallelizable]
    public async Task GenerateAsync_PdfFormat_ReturnsPdfDocument()
    {
        var (dataService, context, connection) = ReportPluginTestDataHelper.CreateDataService();
        using (connection)
        using (context)
        {
            var itsmSource = "servicenow";
            var company = "Acme";
            var snapshotDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
            await ReportPluginTestDataHelper.SeedTicketAsync(
                dataService,
                itsmSource,
                company,
                "INC1002",
                snapshotDate,
                ("Number", "INC1002"),
                ("State", "Closed"),
                ("Priority", "Low"),
                ("Opened At", "2026-01-01"),
                ("Resolved At", "2026-01-15"),
                ("Short Description", "Minor issue"));

            var plugin = new TicketDocumentExportPlugin(dataService);
            var request = new ReportRequest
            {
                Parameters = new Dictionary<string, string>
                {
                    ["itsm_source"] = itsmSource,
                    ["company"] = company,
                    ["as_of_date"] = snapshotDate.ToString("yyyy-MM-dd"),
                    ["output_format"] = TicketDocumentExportPlugin.FormatPdf,
                    ["ticket_keys"] = "INC1002",
                    ["fields"] = "Number,State,Priority,Short Description"
                }
            };

            var result = await plugin.GenerateAsync(request);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.OutputType, Is.EqualTo(ReportOutputType.FileDownload));
            Assert.That(result.ContentType, Is.EqualTo("application/pdf"));
            Assert.That(result.FileName, Does.EndWith(".pdf"));
            Assert.That(result.FileContent, Is.Not.Null);
            Assert.That(Encoding.ASCII.GetString(result.FileContent!.Take(5).ToArray()), Is.EqualTo("%PDF-"));
        }
    }
}
