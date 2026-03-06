namespace UPMS.Web.Tests;

/// <summary>
/// Requirements for the snapshots page (/snapshots).
/// </summary>
[TestFixture]
public class SnapshotPageTests : PageTestBase
{
    [Test]
    public async Task WhenSnapshotsPageLoadsThenPageTitleContainsSnapshots()
    {
        // Act
        await Page.GotoAsync(Url("/snapshots"));

        // Assert
        string title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Snapshots").IgnoreCase);
    }

    [Test]
    public async Task WhenSnapshotsPageLoadsThenHeadingIsVisible()
    {
        // Act
        await Page.GotoAsync(Url("/snapshots"));

        // Assert
        var heading = Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Snapshots" });
        await Assertions.Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenSnapshotsPageLoadsThenSnapshotTableIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/snapshots"));

        // Assert
        var table = Page.Locator("table[data-testid='snapshot-table']");
        await Assertions.Expect(table).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenSnapshotsPageLoadsThenTableHasExpectedColumnHeaders()
    {
        // Act
        await Page.GotoAsync(Url("/snapshots"));

        // Assert
        var headers = Page.Locator("table[data-testid='snapshot-table'] th");

        await Assertions.Expect(headers.Filter(new() { HasText = "ITSM Source" }).First).ToBeVisibleAsync();
        await Assertions.Expect(headers.Filter(new() { HasText = "Snapshot Date" }).First).ToBeVisibleAsync();
        await Assertions.Expect(headers.Filter(new() { HasText = "Uploaded By" }).First).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenSnapshotsPageLoadsThenItsmSourceFilterIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/snapshots"));

        // Assert
        var filter = Page.Locator("[data-testid='snapshot-itsm-filter']");
        await Assertions.Expect(filter).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenSnapshotsPageHasNoDataThenEmptyStateMessageIsShown()
    {
        // Act
        await Page.GotoAsync(Url("/snapshots"));

        // Assert — when no snapshots are loaded, an empty-state message should appear.
        var emptyState = Page.Locator("[data-testid='snapshot-table-empty']");
        await Assertions.Expect(emptyState).ToBeVisibleAsync();
    }
}
