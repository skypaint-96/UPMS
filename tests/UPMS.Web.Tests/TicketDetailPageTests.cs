namespace UPMS.Web.Tests;

/// <summary>
/// Requirements for the ticket detail page (/tickets/{company}/{ticketKey}).
/// </summary>
[TestFixture]
public class TicketDetailPageTests : PageTestBase
{
    // A representative ticket key used by requirement tests. The actual
    // ticket does not need to exist in the database for structural checks.
    private const string SampleCompany = "Acme-Corp";
    private const string SampleTicketKey = "INC0001234";

    private string TicketDetailUrl => Url($"/tickets/{SampleCompany}/{SampleTicketKey}");

    [Test]
    public async Task WhenTicketDetailLoadsThenPageTitleContainsTicketKey()
    {
        // Act
        await Page.GotoAsync(TicketDetailUrl);

        // Assert
        string title = await Page.TitleAsync();
        Assert.That(title, Does.Contain(SampleTicketKey));
    }

    [Test]
    public async Task WhenTicketDetailLoadsThenHeadingShowsTicketKey()
    {
        // Act
        await Page.GotoAsync(TicketDetailUrl);

        // Assert
        var heading = Page.Locator("h1");
        await Assertions.Expect(heading).ToContainTextAsync(SampleTicketKey);
    }

    [Test]
    public async Task WhenTicketDetailLoadsThenFieldsSectionIsVisible()
    {
        // Act
        await Page.GotoAsync(TicketDetailUrl);

        // Assert — a section listing the ticket's current field values.
        var fieldsSection = Page.Locator("[data-testid='ticket-fields']");
        await Assertions.Expect(fieldsSection).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketDetailLoadsThenFieldHistorySectionIsVisible()
    {
        // Act
        await Page.GotoAsync(TicketDetailUrl);

        // Assert — a section showing the field change history timeline.
        var historySection = Page.Locator("[data-testid='field-history']");
        await Assertions.Expect(historySection).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketDetailLoadsThenCompanyNameIsDisplayed()
    {
        // Act
        await Page.GotoAsync(TicketDetailUrl);

        // Assert
        var companyElement = Page.Locator("[data-testid='ticket-company']");
        await Assertions.Expect(companyElement).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketDetailLoadsThenItsmSourceIsDisplayed()
    {
        // Act
        await Page.GotoAsync(TicketDetailUrl);

        // Assert
        var itsmElement = Page.Locator("[data-testid='ticket-itsm-source']");
        await Assertions.Expect(itsmElement).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenTicketDetailLoadsThenBackToListLinkIsPresent()
    {
        // Act
        await Page.GotoAsync(TicketDetailUrl);

        // Assert — there should be a link in the page content to navigate back to the tickets list.
        var backLink = Page.Locator("main a[href='/tickets']");
        await Assertions.Expect(backLink).ToBeVisibleAsync();
    }
}
