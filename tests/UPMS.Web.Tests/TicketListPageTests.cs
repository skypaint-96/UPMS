namespace UPMS.Web.Tests;

/// <summary>
/// Requirements for the ticket listing page (/tickets).
/// </summary>
[TestFixture]
public class TicketListPageTests : PageTestBase
{
    [Test]
    public async Task WhenTicketsPageLoadsThenPageTitleContainsTickets()
    {
        // Act
        await Page.GotoAsync(Url("/tickets"));

        // Assert
        string title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Tickets").IgnoreCase);
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenHeadingIsVisible()
    {
        // Act
        await Page.GotoAsync(Url("/tickets"));

        // Assert
        var heading = Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Tickets" });
        await Assertions.Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenTicketTableIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/tickets"));

        // Assert — a <table> element should be rendered for listing tickets.
        var table = Page.Locator("table[data-testid='ticket-table']");
        await Assertions.Expect(table).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenTableHasExpectedColumnHeaders()
    {
        // Act
        await Page.GotoAsync(Url("/tickets"));

        // Assert — the ticket table must include these core columns.
        var headers = Page.Locator("table[data-testid='ticket-table'] th");

        await Assertions.Expect(headers.Filter(new() { HasText = "Ticket Key" }).First).ToBeVisibleAsync();
        await Assertions.Expect(headers.Filter(new() { HasText = "Company" }).First).ToBeVisibleAsync();
        await Assertions.Expect(headers.Filter(new() { HasText = "ITSM Source" }).First).ToBeVisibleAsync();
        await Assertions.Expect(headers.Filter(new() { HasText = "Status" }).First).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenCompanyFilterIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/tickets"));

        // Assert — a company filter control should be available.
        var filter = Page.Locator("[data-testid='company-filter']");
        await Assertions.Expect(filter).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenItsmSourceFilterIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/tickets"));

        // Assert — an ITSM source filter control should be available.
        var filter = Page.Locator("[data-testid='itsm-source-filter']");
        await Assertions.Expect(filter).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageHasNoDataThenEmptyStateMessageIsShown()
    {
        // Act
        await Page.GotoAsync(Url("/tickets"));

        // Assert — when no tickets are loaded, an empty-state message should appear.
        var emptyState = Page.Locator("[data-testid='ticket-table-empty']");
        await Assertions.Expect(emptyState).ToBeVisibleAsync();
    }
}
