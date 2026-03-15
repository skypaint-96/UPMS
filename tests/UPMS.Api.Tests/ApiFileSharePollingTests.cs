namespace UPMS.Api.Tests;

using System.Net;
using System.Net.Http.Json;

[TestFixture]
public sealed class ApiFileSharePollingTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task File_share_polling_settings_endpoint_returns_bound_configuration()
    {
        var response = await _client.GetAsync("/api/v1/file-share-polling/settings");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<FileSharePollingSettingsDto>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Enabled, Is.True);
        Assert.That(payload.AllowUserManagedSources, Is.True);
        Assert.That(payload.AllowedWatchedRoots, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Can_create_list_and_run_file_share_polling_source_from_the_api()
    {
        string root = Path.Combine(_factory.PollingRoot, Guid.NewGuid().ToString("N"));
        string watch = Path.Combine(root, "watch");
        string archive = Path.Combine(root, "archive");
        string error = Path.Combine(root, "error");

        var createResponse = await _client.PostAsJsonAsync("/api/v1/file-share-polling-sources", new
        {
            name = "Nightly ServiceNow drop",
            enabled = true,
            watchedPath = watch,
            filePatterns = new[] { "*.csv", "*.json" },
            archivePath = archive,
            errorPath = error,
            itsmSource = "servicenow-prod",
            pollIntervalSeconds = 300,
            maxFilesPerCycle = 25,
            stableFileAgeSeconds = 30,
        });

        Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await createResponse.Content.ReadFromJsonAsync<FileSharePollingSourceDto>();
        Assert.That(created, Is.Not.Null);
        Assert.That(created!.Name, Is.EqualTo("Nightly ServiceNow drop"));
        Assert.That(created.ItsmSource, Is.EqualTo("servicenow-prod"));

        var list = await _client.GetFromJsonAsync<List<FileSharePollingSourceDto>>("/api/v1/file-share-polling-sources");
        Assert.That(list, Is.Not.Null);
        Assert.That(list!.Any(source => source.Id == created.Id), Is.True);

        var firstRun = await _client.PostAsync($"/api/v1/file-share-polling-sources/{created.Id}/run", null);
        Assert.That(firstRun.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        var firstPayload = await firstRun.Content.ReadFromJsonAsync<FileSharePollingRunDto>();
        Assert.That(firstPayload, Is.Not.Null);
        Assert.That(firstPayload!.AlreadyQueued, Is.False);
        Assert.That(firstPayload.Job.JobType, Is.EqualTo("file-share-poll"));

        var secondRun = await _client.PostAsync($"/api/v1/file-share-polling-sources/{created.Id}/run", null);
        Assert.That(secondRun.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        var secondPayload = await secondRun.Content.ReadFromJsonAsync<FileSharePollingRunDto>();
        Assert.That(secondPayload, Is.Not.Null);
        Assert.That(secondPayload!.AlreadyQueued, Is.True);
        Assert.That(secondPayload.Job.Id, Is.EqualTo(firstPayload.Job.Id));
    }

    private sealed record FileSharePollingSettingsDto(
        bool Enabled,
        bool AllowUserManagedSources,
        List<string> AllowedWatchedRoots,
        List<string> AllowedArchiveRoots,
        List<string> AllowedErrorRoots);

    private sealed record FileSharePollingSourceDto(
        Guid Id,
        string Name,
        bool Enabled,
        string WatchedPath,
        List<string> FilePatterns,
        string ArchivePath,
        string ErrorPath,
        string ItsmSource,
        int PollIntervalSeconds,
        int? MaxFilesPerCycle,
        int StableFileAgeSeconds);

    private sealed record BackgroundJobDto(Guid Id, string JobType, string Status);

    private sealed record FileSharePollingRunDto(BackgroundJobDto Job, bool AlreadyQueued);
}
