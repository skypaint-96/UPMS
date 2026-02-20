namespace UPMS.Web.Tests;

/// <summary>
/// Requirements for site-wide navigation behaviour.
/// </summary>
[TestFixture]
public class NavigationTests : PageTestBase
{
    [Test]
    public async Task WhenPageLoadsThenNavContainsHomeLink()
    {
        // Act
        await Page.GotoAsync(Url("/"));

        // Assert
        var homeLink = Page.Locator("nav a[href='/']");
        await Assertions.Expect(homeLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenPageLoadsThenNavContainsTicketsLink()
    {
        // Act
        await Page.GotoAsync(Url("/"));

        // Assert
        var ticketsLink = Page.Locator("nav a[href='/tickets']");
        await Assertions.Expect(ticketsLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenPageLoadsThenNavContainsSnapshotsLink()
    {
        // Act
        await Page.GotoAsync(Url("/"));

        // Assert
        var snapshotsLink = Page.Locator("nav a[href='/snapshots']");
        await Assertions.Expect(snapshotsLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsLinkClickedThenNavigatesToTicketsPage()
    {
        // Arrange
        await Page.GotoAsync(Url("/"));

        // Act
        await Page.Locator("nav a[href='/tickets']").ClickAsync();

        // Assert
        await Page.WaitForURLAsync("**/tickets");
        Assert.That(Page.Url, Does.Contain("/tickets"));
    }

    [Test]
    public async Task WhenSnapshotsLinkClickedThenNavigatesToSnapshotsPage()
    {
        // Arrange
        await Page.GotoAsync(Url("/"));

        // Act
        await Page.Locator("nav a[href='/snapshots']").ClickAsync();

        // Assert
        await Page.WaitForURLAsync("**/snapshots");
        Assert.That(Page.Url, Does.Contain("/snapshots"));
    }

    [Test]
    public async Task WhenUnknownRouteVisitedThenNotFoundPageDisplayed()
    {
        // Act
        await Page.GotoAsync(Url("/this-route-does-not-exist"));

        // Assert � the not-found page should be shown with an informative message.
        var notFoundHeading = Page.Locator("text=Not Found");
        await Assertions.Expect(notFoundHeading).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenOnTicketsPageThenTicketsNavLinkIsActive()
    {
        // Act
        await Page.GotoAsync(Url("/tickets"));

        // Assert � the active nav link should have an "active" CSS class or
        // aria-current attribute to indicate the current page.
        var ticketsLink = Page.Locator("nav a[href='/tickets']");
        await Assertions.Expect(ticketsLink).ToHaveAttributeAsync("aria-current", "page");
    }

    [Test]
    public async Task WhenPageLoadsThenNavContainsUploadLink()
    {
        // Act
        await Page.GotoAsync(Url("/"));

        // Assert
        var uploadLink = Page.Locator("nav a[href='/upload']");
        await Assertions.Expect(uploadLink).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenUploadLinkClickedThenNavigatesToUploadPage()
    {
        // Arrange
        await Page.GotoAsync(Url("/"));

        // Act
        await Page.Locator("nav a[href='/upload']").ClickAsync();

        // Assert
        await Page.WaitForURLAsync("**/upload");
        Assert.That(Page.Url, Does.Contain("/upload"));
    }

    [Test]
    public async Task Navigation_HasReportStoreLink_WhenImplemented()
    {
        // Arrange
        await Page.GotoAsync(Url("/"));

        // Assert
        var reportsLink = Page.Locator("nav a[href='/reports'], nav a:has-text('Reports')");
        await Assertions.Expect(reportsLink).ToBeVisibleAsync();
    }
}
