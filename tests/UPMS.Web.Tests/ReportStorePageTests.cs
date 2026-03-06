namespace UPMS.Web.Tests;

using Microsoft.Playwright;

/// <summary>
/// End-to-end tests for the Report Store page (/reports).
/// Implemented in Stage 5.
/// </summary>
[TestFixture]
public class ReportStorePageTests : PageTestBase
{
    [Test]
    public async Task ReportStorePage_NavigatingTo_ShowsNotFoundOrReports()
    {
        // Act
        await Page.GotoAsync(Url("/reports"));

        // Assert — the page now exists; title must contain "Report Store"
        var title = await Page.TitleAsync();
        Assert.That(
            title.Contains("Report Store", StringComparison.OrdinalIgnoreCase),
            Is.True,
            $"Expected page title to contain 'Report Store' but was '{title}'.");
    }

    [Test]
    public async Task ReportStorePage_WhenImplemented_HasHeading()
    {
        // Arrange
        await Page.GotoAsync(Url("/reports"));

        // Act
        var heading = Page.Locator("h1");

        // Assert
        await Assertions.Expect(heading).ToContainTextAsync("Report Store");
    }

    [Test]
    public async Task ReportStorePage_WhenImplemented_ListsPlugins()
    {
        // Arrange
        await Page.GotoAsync(Url("/reports"));

        // Act
        var pluginList = Page.Locator("[data-testid='plugin-list']");

        // Assert
        await Assertions.Expect(pluginList).ToBeVisibleAsync();
    }

    [Test]
    public async Task ReportStorePage_WhenImplemented_ShowsPluginDisplayName()
    {
        // Arrange
        await Page.GotoAsync(Url("/reports"));

        // Assert — the StubReportPlugin's DisplayName must appear on the page
        await Assertions.Expect(Page.Locator("text=Stub Report")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ReportStorePage_WhenImplemented_ShowsPluginDescription()
    {
        // Arrange
        await Page.GotoAsync(Url("/reports"));

        // Assert — the StubReportPlugin's Description must appear on the page
        await Assertions.Expect(Page.Locator("text=no-op stub plugin")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ReportStorePage_WhenImplemented_HasParameterForm()
    {
        // Arrange
        await Page.GotoAsync(Url("/reports"));

        // Act — click the Generate Report button for the stub plugin
        await Page.Locator("button:has-text('Generate Report')").First.ClickAsync();

        // Assert
        var parameterForm = Page.Locator("[data-testid='parameter-form']");
        await Assertions.Expect(parameterForm).ToBeVisibleAsync();
    }

    [Test]
    public async Task ReportStorePage_WhenImplemented_NavContainsReportsLink()
    {
        // Arrange
        await Page.GotoAsync(Url("/"));

        // Assert — nav must contain a link to /reports or with text "Reports"
        var reportsLink = Page.Locator("nav a[href='/reports'], nav a:has-text('Reports')");
        await Assertions.Expect(reportsLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task ReportStorePage_WhenImplemented_PluginGeneratesResult()
    {
        // Arrange
        await Page.GotoAsync(Url("/reports"));

        // Act — open the parameter form for the stub plugin
        await Page.Locator("button:has-text('Generate Report')").First.ClickAsync();

        // Fill in required parameters
        await Page.Locator("[data-testid='parameter-form'] input[type='text']").First.FillAsync("Acme Corp");
        await Page.Locator("[data-testid='parameter-form'] input[type='date']").First.FillAsync(DateTime.Today.ToString("yyyy-MM-dd"));

        // Submit
        await Page.Locator("[data-testid='parameter-form'] button:has-text('Generate')").ClickAsync();

        // Assert
        var resultSection = Page.Locator("[data-testid='report-result']");
        await Assertions.Expect(resultSection).ToBeVisibleAsync();
    }
}
