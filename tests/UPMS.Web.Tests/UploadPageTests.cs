namespace UPMS.Web.Tests;

/// <summary>
/// Requirements for the ticket dump upload page (/upload).
/// Users must be able to upload CSV or JSON ticket dumps from ITSM sources.
/// </summary>
[TestFixture]
public class UploadPageTests : PageTestBase
{
    [Test]
    public async Task WhenUploadPageLoadsThenPageTitleContainsUpload()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert
        string title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Upload").IgnoreCase);
    }

    [Test]
    public async Task WhenUploadPageLoadsThenHeadingIsVisible()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert
        var heading = Page.GetByRole(Microsoft.Playwright.AriaRole.Heading, new() { Name = "Upload" });
        await Assertions.Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenUploadPageLoadsThenItsmSourceSelectorIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert � users must select which ITSM source the dump originates from.
        var selector = Page.Locator("[data-testid='upload-itsm-source']");
        await Assertions.Expect(selector).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenUploadPageLoadsThenFileInputIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert � a file input must be available for selecting CSV or JSON files.
        var fileInput = Page.Locator("[data-testid='upload-file-input']");
        await Assertions.Expect(fileInput).ToBeAttachedAsync();
    }

    [Test]
    public async Task WhenUploadPageLoadsThenUploadButtonIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert
        var button = Page.Locator("[data-testid='upload-submit']");
        await Assertions.Expect(button).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenUploadPageLoadsThenResultAreaIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert � an area must exist to display upload results or status messages.
        var resultArea = Page.Locator("[data-testid='upload-result']");
        await Assertions.Expect(resultArea).ToBeAttachedAsync();
    }

    [Test]
    public async Task WhenUploadSubmittedWithoutFileThenValidationMessageShown()
    {
        // Arrange
        await Page.GotoAsync(Url("/upload"));

        // Act � click upload without selecting a file.
        await Page.Locator("[data-testid='upload-submit']").ClickAsync();

        // Assert � a validation message should tell the user a file is required.
        var validation = Page.Locator("[data-testid='upload-validation']");
        await Assertions.Expect(validation).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenUploadSubmittedWithoutItsmSourceThenValidationMessageShown()
    {
        // Arrange
        await Page.GotoAsync(Url("/upload"));

        // Act � click upload without selecting an ITSM source.
        await Page.Locator("[data-testid='upload-submit']").ClickAsync();

        // Assert
        var validation = Page.Locator("[data-testid='upload-validation']");
        await Assertions.Expect(validation).ToBeVisibleAsync();
    }
    [Test]
    public async Task UploadPage_HasCompanyNameInput()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert — users must supply a company name so tickets are attributed correctly.
        var companyInput = Page.Locator("[data-testid='company-name-input'], input[name='companyName']");
        var hasTestId = await companyInput.CountAsync() > 0;
        var hasLabel = await Page.Locator("label", new() { HasText = "Company" }).CountAsync() > 0;
        Assert.That(hasTestId || hasLabel, Is.True, "Expected a company name input or a label containing 'Company'.");
    }

    [Test]
    public async Task UploadPage_HasSnapshotDateInput()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert — users must supply the date the snapshot was taken.
        var dateInput = Page.Locator("[data-testid='snapshot-date-input'], input[type='date']");
        await Assertions.Expect(dateInput.First).ToBeAttachedAsync();
    }

    [Test]
    [Description("Stage 4 requirement — ingest summary is shown after a successful upload.")]
    public Task UploadPage_ShowsIngestSummary_AfterSuccessfulUpload()
    {
        Assert.Ignore("Requires a real database connection — run against a live environment");
        return Task.CompletedTask;
    }

    [Test]
    [Description("Stage 4 requirement — summary must display how many tickets were ingested.")]
    public Task UploadPage_ShowsTicketCount_InSummary()
    {
        Assert.Ignore("Requires a real database connection — run against a live environment");
        return Task.CompletedTask;
    }

    [Test]
    [Description("Stage 4 requirement — summary must display how many field changes were recorded.")]
    public Task UploadPage_ShowsFieldChangeCount_InSummary()
    {
        Assert.Ignore("Requires a real database connection — run against a live environment");
        return Task.CompletedTask;
    }

    [Test]
    public async Task UploadPage_ValidationMessage_ShownForMissingCompanyName()
    {
        // Arrange
        await Page.GotoAsync(Url("/upload"));

        // Act — click upload without entering a company name.
        await Page.Locator("[data-testid='upload-submit']").ClickAsync();

        // Assert — validation message should indicate company name is required.
        var validation = Page.Locator("[data-testid='upload-validation']");
        await Assertions.Expect(validation).ToBeVisibleAsync();
    }
}
