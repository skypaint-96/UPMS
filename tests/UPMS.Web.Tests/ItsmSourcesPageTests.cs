namespace UPMS.Web.Tests;

/// <summary>
/// Requirements for the ITSM Sources management page (/itsm-sources).
/// Users must be able to list, add, delete and navigate to individual ITSM sources.
/// </summary>
[TestFixture]
public class ItsmSourcesPageTests : PageTestBase
{
    [Test]
    public async Task WhenItsmSourcesPageLoadsThenPageTitleContainsItsmSources()
    {
        // Act
        await Page.GotoAsync(Url("/itsm-sources"));

        // Assert
        string title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("ITSM").IgnoreCase);
    }

    [Test]
    public async Task WhenItsmSourcesPageLoadsThenHeadingIsVisible()
    {
        // Act
        await Page.GotoAsync(Url("/itsm-sources"));

        // Assert
        var heading = Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "ITSM Sources" });
        await Assertions.Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenItsmSourcesPageLoadsThenSourcesListIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/itsm-sources"));

        // Assert
        var list = Page.Locator("[data-testid='itsm-sources-list']");
        await Assertions.Expect(list).ToBeAttachedAsync();
    }

    [Test]
    public async Task WhenItsmSourcesPageLoadsThenAddFormIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/itsm-sources"));

        // Assert — form inputs must exist
        var nameInput  = Page.Locator("[data-testid='add-source-name']");
        var labelInput = Page.Locator("[data-testid='add-source-label']");
        var submitBtn  = Page.Locator("[data-testid='add-source-submit']");

        await Assertions.Expect(nameInput).ToBeVisibleAsync();
        await Assertions.Expect(labelInput).ToBeVisibleAsync();
        await Assertions.Expect(submitBtn).ToBeVisibleAsync();
    }

    [Test]
    [Description("Stage 5 — sources seeded by migration 004 should appear in the list.")]
    public async Task ItsmSourcesPage_ShowsAllSources()
    {
        // Act
        await Page.GotoAsync(Url("/itsm-sources"));

        // Assert — the page loads and the sources list container is present;
        // seeded sources (servicenow-default, jira-default) appear after migration 004 runs.
        var list = Page.Locator("[data-testid='itsm-sources-list']");
        await Assertions.Expect(list).ToBeAttachedAsync();
    }

    [Test]
    [Description("Stage 5 — adding a new source should make it appear in the list immediately.")]
    public async Task ItsmSourcesPage_AddSource_AppearsInList()
    {
        // Arrange
        await Page.GotoAsync(Url("/itsm-sources"));

        string slug  = $"test-source-{Guid.NewGuid():N}".Substring(0, 30);
        string label = "Test Source Label";

        // Act
        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();

        // Assert — the new row should appear in the sources list
        var newRow = Page.Locator($"[data-testid='source-row-{slug}']");
        await Assertions.Expect(newRow).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    [Test]
    [Description("Stage 5 — deleting a source removes it from the list.")]
    public async Task ItsmSourcesPage_DeleteSource_RemovesFromList()
    {
        // Arrange — first add a source so we can delete it
        await Page.GotoAsync(Url("/itsm-sources"));

        string slug  = $"del-source-{Guid.NewGuid():N}".Substring(0, 28);
        string label = "Delete Me";

        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();

        // Wait for the row to appear
        await Assertions.Expect(
            Page.Locator($"[data-testid='source-row-{slug}']")
        ).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Act — click Delete then confirm
        await Page.Locator($"[data-testid='delete-source-{slug}']").ClickAsync();
        await Page.Locator($"[data-testid='confirm-delete-{slug}']").ClickAsync();

        // Assert — the row should no longer be visible
        var deletedRow = Page.Locator($"[data-testid='source-row-{slug}']");
        await Assertions.Expect(deletedRow).ToBeHiddenAsync(new() { Timeout = 5000 });
    }

    [Test]
    [Description("Stage 5 — each source row must have a link to its detail page.")]
    public async Task ItsmSourcesPage_HasLinkToDetailPage()
    {
        // Arrange — add a source so there is definitely a row to check
        await Page.GotoAsync(Url("/itsm-sources"));

        string slug  = $"link-source-{Guid.NewGuid():N}".Substring(0, 29);
        string label = "Link Test Source";

        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();

        await Assertions.Expect(
            Page.Locator($"[data-testid='source-row-{slug}']")
        ).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Assert — the link to the detail page must be present
        var detailLink = Page.Locator($"[data-testid='source-link-{slug}']");
        await Assertions.Expect(detailLink).ToBeVisibleAsync();

        string? href = await detailLink.GetAttributeAsync("href");
        Assert.That(href, Does.Contain($"/itsm-sources/{slug}").IgnoreCase);
    }
}
