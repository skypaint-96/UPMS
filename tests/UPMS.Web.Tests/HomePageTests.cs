namespace UPMS.Web.Tests;

/// <summary>
/// Requirements for the UPMS home / dashboard page.
/// </summary>
[TestFixture]
public class HomePageTests : PageTestBase
{
    [Test]
    public async Task WhenHomePageLoadsThenPageTitleContainsUpms()
    {
        // Act
        await Page.GotoAsync(Url("/"));

        // Assert
        string title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("UPMS").IgnoreCase);
    }

    [Test]
    public async Task WhenHomePageLoadsThenHeadingIsVisible()
    {
        // Act
        await Page.GotoAsync(Url("/"));

        // Assert — the home page should contain a visible heading.
        var heading = Page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenHomePageLoadsThenNavigationIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/"));

        // Assert — a <nav> element should be rendered for site-wide navigation.
        var nav = Page.Locator("nav");
        await Assertions.Expect(nav).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenHomePageLoadsThenDashboardSummaryAreaExists()
    {
        // Act
        await Page.GotoAsync(Url("/"));

        // Assert — the home page should contain a summary/dashboard section
        // that will display ticket statistics or recent activity.
        var dashboard = Page.Locator("[data-testid='dashboard-summary']");
        await Assertions.Expect(dashboard).ToBeVisibleAsync();
    }
}
