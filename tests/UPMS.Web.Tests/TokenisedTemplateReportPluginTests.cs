namespace UPMS.Web.Tests;

using System.Text;
using UPMS.Web.Plugins;
using UPMS.Web.Plugins.Templates;
using UPMS.Web.Templates;

[TestFixture]
public class TokenisedTemplateReportPluginTests
{
    [Test]
    [NonParallelizable]
    public async Task GenerateAsync_HtmlTemplate_ReturnsRenderedHtmlPreview()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"upms-template-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var store = new FileSystemReportTemplateStore(tempDir);
            await store.SaveAsync(
                new ReportTemplateUploadRequest
                {
                    DisplayName = "Lifecycle Html",
                    Kind = ReportTemplateKind.Document,
                    OriginalFileName = "lifecycle.html"
                },
                new MemoryStream(Encoding.UTF8.GetBytes("<html><body><h1>{{company}}</h1>{{tickets_html_table}}</body></html>")));

            var template = store.GetAllTemplates().Single();
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
                    "INC2001",
                    snapshotDate,
                    ("Number", "INC2001"),
                    ("State", "Open"),
                    ("Priority", "High"),
                    ("Assigned To", "Alice"),
                    ("Short Description", "Template test"),
                    ("Opened At", "2026-01-04"));

                var plugin = new TokenisedTemplateReportPlugin(dataService, store);
                var request = new ReportRequest
                {
                    Parameters = new Dictionary<string, string>
                    {
                        ["template_id"] = template.Id,
                        ["itsm_source"] = itsmSource,
                        ["company"] = company,
                        ["as_of_date"] = snapshotDate.ToString("yyyy-MM-dd"),
                        ["detail_fields"] = "State,Priority,Assigned To,Short Description"
                    }
                };

                var result = await plugin.GenerateAsync(request);

                Assert.That(result.Success, Is.True, result.ErrorMessage);
                Assert.That(result.OutputType, Is.EqualTo(ReportOutputType.HtmlContent));
                Assert.That(result.HtmlContent, Does.Contain("<h1>Acme</h1>"));
                Assert.That(result.HtmlContent, Does.Contain("INC2001"));
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
