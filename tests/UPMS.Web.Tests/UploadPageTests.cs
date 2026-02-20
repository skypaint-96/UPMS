namespace UPMS.Web.Tests;

/// <summary>
/// Requirements for the ticket dump upload page (/upload).
/// Users must be able to upload CSV or JSON ticket dumps from ITSM sources.
/// Company is extracted from the uploaded data — it is no longer a form input.
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

        // Assert — users must select which ITSM source the dump originates from.
        var selector = Page.Locator("[data-testid='upload-itsm-source']");
        await Assertions.Expect(selector).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenUploadPageLoadsThenNoCompanyNameInputPresent()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert — company is read from the CSV data, not entered on the form.
        var companyInput = Page.Locator("[data-testid='company-name-input']");
        Assert.That(await companyInput.CountAsync(), Is.EqualTo(0),
            "Company name input should not be present; company is extracted from uploaded data.");
    }

    [Test]
    public async Task WhenUploadPageLoadsThenItsmSourceDropdownHasOptions()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert — the dropdown should contain at least the placeholder option
        // (populated from the database via IItsmSourceService).
        var dropdown = Page.Locator("[data-testid='upload-itsm-source']");
        await Assertions.Expect(dropdown).ToBeVisibleAsync();

        // At minimum there is the placeholder "-- Select ITSM Source --" option
        int optionCount = await Page.Locator("[data-testid='upload-itsm-source'] option").CountAsync();
        Assert.That(optionCount, Is.GreaterThanOrEqualTo(1),
            "ITSM source dropdown must contain at least the placeholder option.");
    }

    [Test]
    public async Task WhenUploadPageLoadsThenFileInputIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert
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

        // Assert
        var resultArea = Page.Locator("[data-testid='upload-result']");
        await Assertions.Expect(resultArea).ToBeAttachedAsync();
    }

    [Test]
    public async Task WhenUploadPageLoadsThenSnapshotDateInputIsPresent()
    {
        // Act
        await Page.GotoAsync(Url("/upload"));

        // Assert — users must supply the date the snapshot was taken.
        var dateInput = Page.Locator("[data-testid='snapshot-date-input'], input[type='date']");
        await Assertions.Expect(dateInput.First).ToBeAttachedAsync();
    }

    [Test]
    public async Task WhenUploadSubmittedWithoutItsmSourceThenValidationMessageShown()
    {
        // Arrange
        await Page.GotoAsync(Url("/upload"));

        // Act — click upload without selecting an ITSM source.
        await Page.Locator("[data-testid='upload-submit']").ClickAsync();

        // Assert
        var validation = Page.Locator("[data-testid='upload-validation']");
        await Assertions.Expect(validation).ToBeVisibleAsync();
    }

    [Test]
    public async Task WhenUploadSubmittedWithoutFileThenValidationMessageShown()
    {
        // Arrange
        await Page.GotoAsync(Url("/upload"));

        // Act — click upload without selecting a file.
        await Page.Locator("[data-testid='upload-submit']").ClickAsync();

        // Assert
        var validation = Page.Locator("[data-testid='upload-validation']");
        await Assertions.Expect(validation).ToBeVisibleAsync();
    }

    [Test]
    [Description("Stage 5 requirement — ingest summary is shown after a successful upload.")]
    public Task UploadPage_ShowsIngestSummary_AfterSuccessfulUpload()
    {
        Assert.Ignore("Requires a real database connection — run against a live environment");
        return Task.CompletedTask;
    }

    [Test]
    [Description("Stage 5 requirement — summary must display how many tickets were ingested.")]
    public Task UploadPage_ShowsTicketCount_InSummary()
    {
        Assert.Ignore("Requires a real database connection — run against a live environment");
        return Task.CompletedTask;
    }

    [Test]
    [Description("Stage 5 requirement — summary must display how many field changes were recorded.")]
    public Task UploadPage_ShowsFieldChangeCount_InSummary()
    {
        Assert.Ignore("Requires a real database connection — run against a live environment");
        return Task.CompletedTask;
    }
}
