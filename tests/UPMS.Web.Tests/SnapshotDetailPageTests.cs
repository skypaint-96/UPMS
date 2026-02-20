namespace UPMS.Web.Tests;

/// <summary>
/// Requirements for the snapshot detail page (/snapshots/{id}).
/// </summary>
[TestFixture]
public class SnapshotDetailPageTests : PageTestBase
{
    private static readonly string SampleSnapshotId = Guid.Empty.ToString();

    private string SnapshotDetailUrl => Url($"/snapshots/{SampleSnapshotId}");

    [Test]
    public async Task WhenSnapshotDetailLoadsThenPageTitleContainsSnapshot()
    {
        // Act
        await Page.GotoAsync(SnapshotDetailUrl);

        // Assert
        string title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Snapshot").IgnoreCase);
    }

    [Test]
    public async Task WhenSnapshotDetailLoadsThenHeadingIsVisible()
    {
        // Act
        await Page.GotoAsync(SnapshotDetailUrl);

        // Assert
        var heading = Page.Locator("h1");
        await Assertions.Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenSnapshotDetailLoadsThenItsmSourceIsDisplayed()
    {
        // Act
        await Page.GotoAsync(SnapshotDetailUrl);

        // Assert
        var itsmElement = Page.Locator("[data-testid='snapshot-itsm-source']");
        await Assertions.Expect(itsmElement).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenSnapshotDetailLoadsThenSnapshotDateIsDisplayed()
    {
        // Act
        await Page.GotoAsync(SnapshotDetailUrl);

        // Assert
        var dateElement = Page.Locator("[data-testid='snapshot-date']");
        await Assertions.Expect(dateElement).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenSnapshotDetailLoadsThenTicketTableIsPresent()
    {
        // Act
        await Page.GotoAsync(SnapshotDetailUrl);

        // Assert — a table listing all tickets in this snapshot.
        var table = Page.Locator("table[data-testid='snapshot-ticket-table']");
        await Assertions.Expect(table).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenSnapshotDetailLoadsThenTicketTableHasExpectedColumns()
    {
        // Act
        await Page.GotoAsync(SnapshotDetailUrl);

        // Assert
        var headers = Page.Locator("table[data-testid='snapshot-ticket-table'] th");

        await Assertions.Expect(headers.Filter(new() { HasText = "Ticket Key" }).First).ToBeVisibleAsync();
        await Assertions.Expect(headers.Filter(new() { HasText = "Company" }).First).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenSnapshotDetailLoadsThenBackToSnapshotsLinkIsPresent()
    {
        // Act
        await Page.GotoAsync(SnapshotDetailUrl);

        // Assert
        var backLink = Page.Locator("main a[href='/snapshots']");
        await Assertions.Expect(backLink).ToBeVisibleAsync();
    }
}
