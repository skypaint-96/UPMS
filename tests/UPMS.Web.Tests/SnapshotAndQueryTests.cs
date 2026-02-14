using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace UPMS.Web.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class SnapshotAndQueryTests : PageTest
{
    [Test]
    public async Task HomePage_HasNavigationLinksToMainFeatures()
    {
        await Page.GotoAsync("https://localhost:54480//");
        var uploadLink = Page.Locator("a[href='upload-snapshot']");
        var queryLink = Page.Locator("a[href='query-tickets']");
        await Expect(uploadLink).ToBeVisibleAsync();
        await Expect(queryLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task UploadSnapshot_FormFieldsAndValidation()
    {
        await Page.GotoAsync("https://localhost:54480//upload-snapshot");
        await Expect(Page.Locator("input[formcontrolname='ItsmSource'],input[name='ItsmSource']")).ToBeVisibleAsync();
        await Expect(Page.Locator("input[formcontrolname='SnapshotDate'],input[name='SnapshotDate']")).ToBeVisibleAsync();
        await Expect(Page.Locator("textarea, input[formcontrolname='TicketsCsv']")).ToBeVisibleAsync();
        // Try submitting empty form
        await Page.ClickAsync("button[type='submit']");
        await Expect(Page.Locator(".validation-summary-errors, .validation-message, .validation-summary")).ToBeVisibleAsync();
    }

    [Test]
    public async Task UploadSnapshot_InvalidCsv_ShowsHelpfulInstructions()
    {
        await Page.GotoAsync("https://localhost:54480//upload-snapshot");
        await Page.FillAsync("input[formcontrolname='ItsmSource'],input[name='ItsmSource']", "TestSource");
        await Page.FillAsync("input[formcontrolname='SnapshotDate'],input[name='SnapshotDate']", "2024-01-01");
        await Page.FillAsync("textarea, input[formcontrolname='TicketsCsv']", "badly,formatted,csv");
        await Page.ClickAsync("button[type='submit']");
        await Expect(Page.Locator("text=Error")).ToBeVisibleAsync();
        // Optionally: check for help text about CSV format
    }

    [Test]
    public async Task UploadSnapshot_ValidCsv_StoresTicketsAndShowsSuccess()
    {
        await Page.GotoAsync("https://localhost:54480//upload-snapshot");
        await Page.FillAsync("input[formcontrolname='ItsmSource'],input[name='ItsmSource']", "TestSource");
        await Page.FillAsync("input[formcontrolname='SnapshotDate'],input[name='SnapshotDate']", "2024-01-01");
        await Page.FillAsync("textarea, input[formcontrolname='TicketsCsv']", "T1,CompanyA\nT2,CompanyB");
        await Page.ClickAsync("button[type='submit']");
        await Expect(Page.Locator("text=Snapshot uploaded")).ToBeVisibleAsync();
    }

    [Test]
    public async Task QueryTickets_FormFieldsAndNoResultsMessage()
    {
        await Page.GotoAsync("https://localhost:54480//query-tickets");
        await Expect(Page.Locator("input[formcontrolname='ItsmSource'],input[name='ItsmSource']")).ToBeVisibleAsync();
        await Expect(Page.Locator("input[formcontrolname='CompanyName'],input[name='CompanyName']")).ToBeVisibleAsync();
        await Expect(Page.Locator("input[formcontrolname='AsOfDate'],input[name='AsOfDate']")).ToBeVisibleAsync();
        await Page.ClickAsync("button[type='submit']");
        await Expect(Page.Locator("text=No tickets found")).ToBeVisibleAsync();
    }

    [Test]
    public async Task QueryTickets_WithResults_DisplaysTicketTable()
    {
        // This test assumes tickets exist for the query; in a real test, seed data or mock service
        await Page.GotoAsync("https://localhost:54480//query-tickets");
        await Page.FillAsync("input[formcontrolname='ItsmSource'],input[name='ItsmSource']", "TestSource");
        await Page.FillAsync("input[formcontrolname='CompanyName'],input[name='CompanyName']", "CompanyA");
        await Page.FillAsync("input[formcontrolname='AsOfDate'],input[name='AsOfDate']", "2024-01-01");
        await Page.ClickAsync("button[type='submit']");
        await Expect(Page.Locator("table")).ToBeVisibleAsync();
        await Expect(Page.Locator("td:text('T1')")).ToBeVisibleAsync();
    }
}
