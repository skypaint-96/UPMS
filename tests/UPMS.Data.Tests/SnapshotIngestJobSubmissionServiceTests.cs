namespace UPMS.Data.Tests;

using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UPMS.Data.Artifacts;
using UPMS.Data.Jobs;

[TestFixture]
public sealed class SnapshotIngestJobSubmissionServiceTests
{
    private string _artifactsRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _artifactsRoot = Path.Combine(Path.GetTempPath(), $"upms-submission-tests-{Guid.NewGuid():N}");
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrWhiteSpace(_artifactsRoot) && Directory.Exists(_artifactsRoot))
        {
            try
            {
                Directory.Delete(_artifactsRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of temporary test artifacts.
            }
        }
    }

    [Test]
    public async Task QueueAsync_saves_artifact_and_enqueues_snapshot_job()
    {
        await using var context = new UpmsDbContext(
            new DbContextOptionsBuilder<UpmsDbContext>()
                .UseInMemoryDatabase($"snapshot-submission-{Guid.NewGuid():N}")
                .Options);

        var jobs = new BackgroundJobService(context);
        var artifacts = new FileSystemArtifactStorage(Options.Create(new ArtifactStorageOptions
        {
            RootPath = _artifactsRoot
        }));

        var service = new SnapshotIngestJobSubmissionService(artifacts, jobs);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("number,company,state\nPRB0002,Contoso,Open\n"));

        var queued = await service.QueueAsync(
            stream,
            "snapshot-2026-03-02.csv",
            "text/csv",
            "servicenow-prod",
            new DateOnly(2026, 03, 02),
            "tester");

        var loaded = await jobs.GetByIdAsync(queued.Id);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(loaded!.JobType, Is.EqualTo(BackgroundJobTypes.SnapshotIngest));
        Assert.That(loaded.Status, Is.EqualTo(BackgroundJobStatuses.Pending));

        var payload = JsonSerializer.Deserialize<SnapshotIngestJobPayload>(loaded.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ItsmSource, Is.EqualTo("servicenow-prod"));
        Assert.That(payload.SnapshotDate, Is.EqualTo(new DateOnly(2026, 03, 02)));
        Assert.That(payload.OriginalFileName, Is.EqualTo("snapshot-2026-03-02.csv"));
        Assert.That(artifacts.Exists(payload.ArtifactPath), Is.True);
    }
}
