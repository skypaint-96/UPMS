namespace UPMS.Web.Tests;

/// <summary>
/// Requirements for the ITSM Source detail/mapping-editor page (/itsm-sources/{name}).
/// Users must be able to view, add and delete field mappings for a named ITSM source.
/// </summary>
[TestFixture]
public class ItsmSourceDetailPageTests : PageTestBase
{
    // Each test creates its own uniquely-named source so tests are isolated.

    private async Task<string> CreateSourceAndNavigateAsync(string? slugOverride = null)
    {
        string slug  = slugOverride ?? $"detail-src-{Guid.NewGuid():N}".Substring(0, 30);
        string label = $"Detail Source {slug}";

        // Create the source via the list page
        await Page.GotoAsync(Url("/itsm-sources"));
        await Page.Locator("[data-testid='add-source-name']").FillAsync(slug);
        await Page.Locator("[data-testid='add-source-label']").FillAsync(label);
        await Page.Locator("[data-testid='add-source-submit']").ClickAsync();

        // Wait for the row then navigate to its detail page
        await Assertions.Expect(
            Page.Locator($"[data-testid='source-row-{slug}']")
        ).ToBeVisibleAsync(new() { Timeout = 5000 });

        await Page.GotoAsync(Url($"/itsm-sources/{slug}"));
        return slug;
    }

    // ── ShowsSourceName ────────────────────────────────────────────────────

    [Test]
    [Description("Stage 5 — detail page shows the source display name.")]
    public async Task ItsmSourceDetailPage_ShowsSourceName()
    {
        // Arrange
        string slug = await CreateSourceAndNavigateAsync();

        // Assert
        var displayName = Page.Locator("[data-testid='source-display-name']");
        await Assertions.Expect(displayName).ToBeVisibleAsync();

        string text = await displayName.InnerTextAsync();
        Assert.That(text, Is.Not.Empty);
    }

    // ── ShowsMappingsTable ─────────────────────────────────────────────────

    [Test]
    [Description("Stage 5 — detail page shows the mappings table container.")]
    public async Task ItsmSourceDetailPage_ShowsMappingsTable()
    {
        // Arrange
        await CreateSourceAndNavigateAsync();

        // Assert — table container must be present even when empty
        var table = Page.Locator("[data-testid='mappings-table']");
        await Assertions.Expect(table).ToBeAttachedAsync();
    }

    // ── AddMapping ─────────────────────────────────────────────────────────

    [Test]
    [Description("Stage 5 — adding a mapping causes it to appear in the mappings table.")]
    public async Task ItsmSourceDetailPage_AddMapping_AppearsInTable()
    {
        // Arrange
        await CreateSourceAndNavigateAsync();

        // Act — fill in the add-mapping form
        await Page.Locator("[data-testid='add-mapping-source-field']").FillAsync("number");
        await Page.Locator("[data-testid='add-mapping-canonical-name']").SelectOptionAsync("ticket_key");
        await Page.Locator("[data-testid='add-mapping-submit']").ClickAsync();

        // Assert — mapping row appears
        var mappingRow = Page.Locator("[data-testid='mapping-row-number']");
        await Assertions.Expect(mappingRow).ToBeVisibleAsync(new() { Timeout = 5000 });
    }

    // ── DeleteMapping ──────────────────────────────────────────────────────

    [Test]
    [Description("Stage 5 — deleting a mapping removes it from the table.")]
    public async Task ItsmSourceDetailPage_DeleteMapping_RemovesFromTable()
    {
        // Arrange — add a mapping then delete it
        await CreateSourceAndNavigateAsync();

        await Page.Locator("[data-testid='add-mapping-source-field']").FillAsync("priority");
        await Page.Locator("[data-testid='add-mapping-canonical-name']").SelectOptionAsync("priority");
        await Page.Locator("[data-testid='add-mapping-submit']").ClickAsync();

        await Assertions.Expect(
            Page.Locator("[data-testid='mapping-row-priority']")
        ).ToBeVisibleAsync(new() { Timeout = 5000 });

        // Act
        await Page.Locator("[data-testid='delete-mapping-priority']").ClickAsync();

        // Assert
        var deletedRow = Page.Locator("[data-testid='mapping-row-priority']");
        await Assertions.Expect(deletedRow).ToBeHiddenAsync(new() { Timeout = 5000 });
    }

    // ── HasBackLink ────────────────────────────────────────────────────────

    [Test]
    [Description("Stage 5 — detail page has a link back to /itsm-sources.")]
    public async Task ItsmSourceDetailPage_HasBackLink()
    {
        // Arrange
        await CreateSourceAndNavigateAsync();

        // Assert
        var backLink = Page.Locator("[data-testid='back-to-sources']");
        await Assertions.Expect(backLink).ToBeVisibleAsync();

        string? href = await backLink.GetAttributeAsync("href");
        Assert.That(href, Does.Contain("/itsm-sources").IgnoreCase);
    }

    // ── BackLink navigates ─────────────────────────────────────────────────

    [Test]
    [Description("Stage 5 — clicking the back link returns to the sources list.")]
    public async Task ItsmSourceDetailPage_BackLink_NavigatesToSourcesList()
    {
        // Arrange
        await CreateSourceAndNavigateAsync();

        // Act
        await Page.Locator("[data-testid='back-to-sources']").ClickAsync();

        // Assert
        await Page.WaitForURLAsync("**/itsm-sources");
        Assert.That(Page.Url, Does.Contain("/itsm-sources"));
        Assert.That(Page.Url, Does.Not.Contain("/itsm-sources/"));
    }
}
