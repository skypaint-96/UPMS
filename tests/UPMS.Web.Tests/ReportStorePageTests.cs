namespace UPMS.Web.Tests;

/// <summary>
/// Tests for the Report Store page (/reports).
/// The page does not exist yet — it will be implemented in Stage 5.
/// Test 1 is a transitional test that accepts either outcome (404 or live page).
/// All remaining tests use Assert.Ignore to document Stage 5 requirements.
/// </summary>
[TestFixture]
public class ReportStorePageTests : PageTestBase
{
    [Test]
    public async Task ReportStorePage_NavigatingTo_ShowsNotFoundOrReports()
    {
        // Act
        await Page.GotoAsync(Url("/reports"));

        // Assert — transitional: either the route is registered (title contains "Reports")
        // or the not-found page is displayed. Both are acceptable before Stage 5 is complete.
        var title = await Page.TitleAsync();
        var notFoundVisible = await Page.Locator("text=Not Found").IsVisibleAsync();

        Assert.That(
            title.Contains("Reports", StringComparison.OrdinalIgnoreCase) || notFoundVisible,
            Is.True,
            "Expected either a Reports page title or the Not Found page.");
    }

    [Test]
    public Task ReportStorePage_WhenImplemented_HasHeading()
    {
        Assert.Ignore("Stage 5 not yet implemented — the /reports page must have a visible heading.");
        return Task.CompletedTask;
    }

    [Test]
    public Task ReportStorePage_WhenImplemented_ListsPlugins()
    {
        Assert.Ignore("Stage 5 not yet implemented — the /reports page must list all registered report plugins.");
        return Task.CompletedTask;
    }

    [Test]
    public Task ReportStorePage_WhenImplemented_ShowsPluginDisplayName()
    {
        Assert.Ignore("Stage 5 not yet implemented — each plugin card must display the plugin's DisplayName.");
        return Task.CompletedTask;
    }

    [Test]
    public Task ReportStorePage_WhenImplemented_ShowsPluginDescription()
    {
        Assert.Ignore("Stage 5 not yet implemented — each plugin card must display the plugin's Description.");
        return Task.CompletedTask;
    }

    [Test]
    public Task ReportStorePage_WhenImplemented_HasParameterForm()
    {
        Assert.Ignore("Stage 5 not yet implemented — selecting a plugin must show a parameter input form.");
        return Task.CompletedTask;
    }

    [Test]
    public Task ReportStorePage_WhenImplemented_NavContainsReportsLink()
    {
        Assert.Ignore("Stage 5 not yet implemented — the site navigation must include a link to /reports.");
        return Task.CompletedTask;
    }

    [Test]
    public Task ReportStorePage_WhenImplemented_PluginGeneratesResult()
    {
        Assert.Ignore("Stage 5 not yet implemented — submitting the parameter form must invoke GenerateAsync and display a result.");
        return Task.CompletedTask;
    }
}
