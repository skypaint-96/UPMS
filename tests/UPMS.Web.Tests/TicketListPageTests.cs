namespace UPMS.Web.Tests;

/// <summary>
/// Tests for the ticket listing page (/tickets) covering the new ticket search UI.
/// </summary>
[TestFixture]
public class TicketListPageTests : PageTestBase
{
    [Test]
    public async Task WhenTicketsPageLoadsThenPageTitleContainsTickets()
    {
        await Page.GotoAsync(Url("/tickets"));
        string title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Tickets").IgnoreCase);
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenHeadingIsVisible()
    {
        await Page.GotoAsync(Url("/tickets"));
        var heading = Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Tickets" });
        await Assertions.Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenTicketTableIsPresent()
    {
        await Page.GotoAsync(Url("/tickets"));
        var table = Page.Locator("table[data-testid='ticket-table']");
        await Assertions.Expect(table).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenTableHasExpectedColumnHeaders()
    {
        await Page.GotoAsync(Url("/tickets"));
        var headers = Page.Locator("table[data-testid='ticket-table'] th");
        await Assertions.Expect(headers.Filter(new() { HasText = "Ticket Key" }).First).ToBeVisibleAsync();
        await Assertions.Expect(headers.Filter(new() { HasText = "Company" }).First).ToBeVisibleAsync();
        await Assertions.Expect(headers.Filter(new() { HasText = "ITSM Source" }).First).ToBeVisibleAsync();
        await Assertions.Expect(headers.Filter(new() { HasText = "Status" }).First).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenItsmSourceFilterIsPresent()
    {
        await Page.GotoAsync(Url("/tickets"));
        var filter = Page.Locator("[data-testid='itsm-source-filter']");
        await Assertions.Expect(filter).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenAsOfDateTimeInputIsPresent()
    {
        await Page.GotoAsync(Url("/tickets"));
        var input = Page.Locator("[data-testid='as-of-datetime']");
        await Assertions.Expect(input).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenEmptyStateIsShown()
    {
        await Page.GotoAsync(Url("/tickets"));
        var emptyState = Page.Locator("[data-testid='ticket-table-empty']");
        await Assertions.Expect(emptyState).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenHintIsShownWhenNoSourceSelected()
    {
        await Page.GotoAsync(Url("/tickets"));
        var hint = Page.Locator("[data-testid='tickets-empty-hint']");
        await Assertions.Expect(hint).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketsPageLoadsThenFieldFiltersPanelIsHiddenWithNoSource()
    {
        await Page.GotoAsync(Url("/tickets"));
        var panel = Page.Locator("[data-testid='field-filters-panel']");
        // Expect hidden or detached when no source selected
        await Assertions.Expect(panel).ToBeHiddenAsync(new() { Timeout = 5000 });
    }

    // Interaction tests that require creating a live ITSM source per-test -------------------------------------------------

    [Test]
    public async Task WhenItsmSourceSelectedThenFieldFiltersPanelAppears()
    {
        var slug = ("tkt-src-" + Guid.NewGuid().ToString("N"))[..20];
        var label = "Ticket Test Source";

        await Page.GotoAsync(Url("/itsm-sources"));
        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();
        await Assertions.Expect(Page.Locator($"[data-testid='source-row-{slug}']")).ToBeVisibleAsync(new() { Timeout = 5000 });

        await Page.GotoAsync(Url("/tickets"));
        await Page.Locator("[data-testid='itsm-source-filter']").SelectOptionAsync(slug);

        var panel = Page.Locator("[data-testid='field-filters-panel']");
        await Assertions.Expect(panel).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task WhenItsmSourceSelectedThenSearchButtonIsVisible()
    {
        var slug = ("tkt-src-" + Guid.NewGuid().ToString("N"))[..20];
        var label = "Ticket Test Source";

        await Page.GotoAsync(Url("/itsm-sources"));
        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();
        await Assertions.Expect(Page.Locator($"[data-testid='source-row-{slug}']")).ToBeVisibleAsync(new() { Timeout = 5000 });

        await Page.GotoAsync(Url("/tickets"));
        await Page.Locator("[data-testid='itsm-source-filter']").SelectOptionAsync(slug);

        var searchBtn = Page.Locator("[data-testid='search-btn']");
        await Assertions.Expect(searchBtn).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task WhenItsmSourceSelectedThenAddFilterButtonIsVisible()
    {
        var slug = ("tkt-src-" + Guid.NewGuid().ToString("N"))[..20];
        var label = "Ticket Test Source";

        await Page.GotoAsync(Url("/itsm-sources"));
        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();
        await Assertions.Expect(Page.Locator($"[data-testid='source-row-{slug}']")).ToBeVisibleAsync(new() { Timeout = 5000 });

        await Page.GotoAsync(Url("/tickets"));
        await Page.Locator("[data-testid='itsm-source-filter']").SelectOptionAsync(slug);

        var addBtn = Page.Locator("[data-testid='add-filter-btn']");
        await Assertions.Expect(addBtn).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task WhenAddFilterClickedThenFilterRowAppearsWithIndex0()
    {
        var slug = ("tkt-src-" + Guid.NewGuid().ToString("N"))[..20];
        var label = "Ticket Test Source";

        await Page.GotoAsync(Url("/itsm-sources"));
        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();
        await Assertions.Expect(Page.Locator($"[data-testid='source-row-{slug}']")).ToBeVisibleAsync(new() { Timeout = 5000 });

        await Page.GotoAsync(Url("/tickets"));
        await Page.Locator("[data-testid='itsm-source-filter']").SelectOptionAsync(slug);

        await Page.Locator("[data-testid='add-filter-btn']").ClickAsync();

        var name0 = Page.Locator("[data-testid='field-filter-name-0']");
        var value0 = Page.Locator("[data-testid='field-filter-value-0']");
        await Assertions.Expect(name0).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(value0).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task WhenAddFilterClickedTwiceThenTwoFilterRowsAppear()
    {
        var slug = ("tkt-src-" + Guid.NewGuid().ToString("N"))[..20];
        var label = "Ticket Test Source";

        await Page.GotoAsync(Url("/itsm-sources"));
        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();
        await Assertions.Expect(Page.Locator($"[data-testid='source-row-{slug}']")).ToBeVisibleAsync(new() { Timeout = 5000 });

        await Page.GotoAsync(Url("/tickets"));
        await Page.Locator("[data-testid='itsm-source-filter']").SelectOptionAsync(slug);

        await Page.Locator("[data-testid='add-filter-btn']").ClickAsync();
        await Page.Locator("[data-testid='add-filter-btn']").ClickAsync();

        var name0 = Page.Locator("[data-testid='field-filter-name-0']");
        var value0 = Page.Locator("[data-testid='field-filter-value-0']");
        var name1 = Page.Locator("[data-testid='field-filter-name-1']");
        var value1 = Page.Locator("[data-testid='field-filter-value-1']");

        await Assertions.Expect(name0).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(value0).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(name1).ToBeVisibleAsync(new() { Timeout = 5000 });
        await Assertions.Expect(value1).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task WhenRemoveFilterClickedThenFilterRowDisappears()
    {
        var slug = ("tkt-src-" + Guid.NewGuid().ToString("N"))[..20];
        var label = "Ticket Test Source";

        await Page.GotoAsync(Url("/itsm-sources"));
        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();
        await Assertions.Expect(Page.Locator($"[data-testid='source-row-{slug}']")).ToBeVisibleAsync(new() { Timeout = 5000 });

        await Page.GotoAsync(Url("/tickets"));
        await Page.Locator("[data-testid='itsm-source-filter']").SelectOptionAsync(slug);

        await Page.Locator("[data-testid='add-filter-btn']").ClickAsync();
        var remove0 = Page.Locator("[data-testid='remove-filter-0']");
        await Assertions.Expect(remove0).ToBeVisibleAsync(new() { Timeout = 5000 });

        await remove0.ClickAsync();

        var name0 = Page.Locator("[data-testid='field-filter-name-0']");
        await Assertions.Expect(name0).ToBeHiddenAsync(new() { Timeout = 5000 });
    }

    [Test]
    public async Task WhenSearchClickedWithNoFiltersThenTableIsVisible()
    {
        var slug = ("tkt-src-" + Guid.NewGuid().ToString("N"))[..20];
        var label = "Ticket Test Source";

        await Page.GotoAsync(Url("/itsm-sources"));
        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();
        await Assertions.Expect(Page.Locator($"[data-testid='source-row-{slug}']")).ToBeVisibleAsync(new() { Timeout = 5000 });

        await Page.GotoAsync(Url("/tickets"));
        await Page.Locator("[data-testid='itsm-source-filter']").SelectOptionAsync(slug);

        await Page.Locator("[data-testid='search-btn']").ClickAsync();

        var table = Page.Locator("table[data-testid='ticket-table']");
        await Assertions.Expect(table).ToBeVisibleAsync(new() { Timeout = 5000 });
    }
}
