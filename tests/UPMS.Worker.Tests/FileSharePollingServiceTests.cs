namespace UPMS.Worker.Tests;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UPMS.Data;
using UPMS.Data.Artifacts;
using UPMS.Data.Jobs;
using UPMS.Worker;

[TestFixture]
public sealed class FileSharePollingServiceTests
{
    private string _rootPath = null!;
    private string _watchPath = null!;
    private string _archivePath = null!;
    private string _errorPath = null!;
    private string _artifactRoot = null!;
    private ServiceProvider _services = null!;

    [SetUp]
    public void SetUp()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"upms-file-poller-tests-{Guid.NewGuid():N}");
        _watchPath = Path.Combine(_rootPath, "watch");
        _archivePath = Path.Combine(_rootPath, "archive");
        _errorPath = Path.Combine(_rootPath, "error");
        _artifactRoot = Path.Combine(_rootPath, "artifacts");

        Directory.CreateDirectory(_watchPath);
        Directory.CreateDirectory(_archivePath);
        Directory.CreateDirectory(_errorPath);
        Directory.CreateDirectory(_artifactRoot);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<UpmsDbContext>(options =>
            options.UseInMemoryDatabase($"upms-file-poller-db-{Guid.NewGuid():N}"));
        services.AddScoped<IBackgroundJobService, BackgroundJobService>();
        services.AddScoped<ISnapshotIngestJobSubmissionService, SnapshotIngestJobSubmissionService>();
        services.AddSingleton<IArtifactStorage>(_ => new FileSystemArtifactStorage(Options.Create(new ArtifactStorageOptions
        {
            RootPath = _artifactRoot
        })));

        _services = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _services.Dispose();

        if (Directory.Exists(_rootPath))
        {
            try
            {
                Directory.Delete(_rootPath, recursive: true);
            }
            catch
            {
                // Best-effort cleanup of temporary test folders.
            }
        }
    }

    [Test]
    public async Task PollOnceAsync_discovers_matching_files_and_queues_snapshot_jobs()
    {
        CreateStableFile("snapshot-2026-03-02.csv", "number,company,state\nPRB0002,Contoso,Open\n");
        CreateStableFile("snapshot-2026-03-03.txt", "ignore me");

        var service = CreatePoller(new FileSharePollingOptions
        {
            Enabled = true,
            PollIntervalSeconds = 1,
            WatchedPath = _watchPath,
            FilePatterns = ["*.csv"],
            ArchivePath = _archivePath,
            ErrorPath = _errorPath,
            MaxFilesPerCycle = 10,
            ItsmSource = "servicenow-prod",
            StableFileAgeSeconds = 0
        });

        var result = await service.PollOnceAsync(CancellationToken.None);

        Assert.That(result.DiscoveredCount, Is.EqualTo(1));
        Assert.That(result.QueuedCount, Is.EqualTo(1));
        Assert.That(CountJobs(), Is.EqualTo(1));
        Assert.That(Directory.GetFiles(_archivePath, "*.csv", SearchOption.AllDirectories), Has.Length.EqualTo(1));
        Assert.That(Directory.GetFiles(_errorPath, "*.csv", SearchOption.AllDirectories), Is.Empty);
        Assert.That(File.Exists(Path.Combine(_watchPath, "snapshot-2026-03-03.txt")), Is.True);
    }

    [Test]
    public async Task PollOnceAsync_honours_max_files_per_cycle_limit()
    {
        CreateStableFile("snapshot-2026-03-02-a.csv", "number,company,state\nPRB0002,Contoso,Open\n");
        CreateStableFile("snapshot-2026-03-03-b.csv", "number,company,state\nPRB0003,Contoso,Open\n");
        CreateStableFile("snapshot-2026-03-04-c.csv", "number,company,state\nPRB0004,Contoso,Open\n");

        var service = CreatePoller(new FileSharePollingOptions
        {
            Enabled = true,
            PollIntervalSeconds = 1,
            WatchedPath = _watchPath,
            FilePatterns = ["*.csv"],
            ArchivePath = _archivePath,
            ErrorPath = _errorPath,
            MaxFilesPerCycle = 2,
            ItsmSource = "servicenow-prod",
            StableFileAgeSeconds = 0
        });

        var firstCycle = await service.PollOnceAsync(CancellationToken.None);
        Assert.That(firstCycle.DiscoveredCount, Is.EqualTo(3));
        Assert.That(firstCycle.QueuedCount, Is.EqualTo(2));
        Assert.That(CountJobs(), Is.EqualTo(2));
        Assert.That(Directory.GetFiles(_watchPath, "*.csv", SearchOption.TopDirectoryOnly), Has.Length.EqualTo(1));

        var secondCycle = await service.PollOnceAsync(CancellationToken.None);
        Assert.That(secondCycle.QueuedCount, Is.EqualTo(1));
        Assert.That(CountJobs(), Is.EqualTo(3));
    }

    [Test]
    public async Task PollOnceAsync_moves_duplicate_files_to_quarantine_without_requeueing()
    {
        CreateStableFile("snapshot-2026-03-02.csv", "number,company,state\nPRB0002,Contoso,Open\n");

        var firstService = CreatePoller(new FileSharePollingOptions
        {
            Enabled = true,
            PollIntervalSeconds = 1,
            WatchedPath = _watchPath,
            FilePatterns = ["*.csv"],
            ArchivePath = _archivePath,
            ErrorPath = _errorPath,
            MaxFilesPerCycle = 10,
            ItsmSource = "servicenow-prod",
            StableFileAgeSeconds = 0
        });

        var firstCycle = await firstService.PollOnceAsync(CancellationToken.None);
        Assert.That(firstCycle.QueuedCount, Is.EqualTo(1));
        Assert.That(CountJobs(), Is.EqualTo(1));

        CreateStableFile("snapshot-2026-03-02-duplicate.csv", "number,company,state\nPRB0002,Contoso,Open\n");

        var secondService = CreatePoller(new FileSharePollingOptions
        {
            Enabled = true,
            PollIntervalSeconds = 1,
            WatchedPath = _watchPath,
            FilePatterns = ["*.csv"],
            ArchivePath = _archivePath,
            ErrorPath = _errorPath,
            MaxFilesPerCycle = 10,
            ItsmSource = "servicenow-prod",
            StableFileAgeSeconds = 0
        });

        var secondCycle = await secondService.PollOnceAsync(CancellationToken.None);

        Assert.That(secondCycle.QueuedCount, Is.EqualTo(0));
        Assert.That(secondCycle.QuarantinedCount, Is.EqualTo(1));
        Assert.That(CountJobs(), Is.EqualTo(1));
        Assert.That(Directory.GetFiles(_errorPath, "*.csv", SearchOption.AllDirectories), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task PollOnceAsync_moves_files_with_unresolved_metadata_to_quarantine()
    {
        CreateStableFile("no-date.csv", "number,company,state\nPRB0002,Contoso,Open\n");

        var service = CreatePoller(new FileSharePollingOptions
        {
            Enabled = true,
            PollIntervalSeconds = 1,
            WatchedPath = _watchPath,
            FilePatterns = ["*.csv"],
            ArchivePath = _archivePath,
            ErrorPath = _errorPath,
            MaxFilesPerCycle = 10,
            ItsmSource = "servicenow-prod",
            StableFileAgeSeconds = 0
        });

        var result = await service.PollOnceAsync(CancellationToken.None);

        Assert.That(result.QueuedCount, Is.EqualTo(0));
        Assert.That(result.QuarantinedCount, Is.EqualTo(1));
        Assert.That(CountJobs(), Is.EqualTo(0));
        Assert.That(Directory.GetFiles(_errorPath, "*.csv", SearchOption.AllDirectories), Has.Length.EqualTo(1));
        Assert.That(Directory.GetFiles(_errorPath, "*.metadata.json", SearchOption.AllDirectories), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task PollOnceAsync_submits_files_into_existing_snapshot_ingest_job_flow()
    {
        CreateStableFile("snapshot-2026-03-02.csv", "number,company,state\nPRB0002,Contoso,Open\n");

        var service = CreatePoller(new FileSharePollingOptions
        {
            Enabled = true,
            PollIntervalSeconds = 1,
            WatchedPath = _watchPath,
            FilePatterns = ["*.csv"],
            ArchivePath = _archivePath,
            ErrorPath = _errorPath,
            MaxFilesPerCycle = 10,
            ItsmSource = "servicenow-prod",
            StableFileAgeSeconds = 0
        });

        var result = await service.PollOnceAsync(CancellationToken.None);
        Assert.That(result.QueuedCount, Is.EqualTo(1));

        using var scope = _services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
        var queuedJob = (await jobs.GetRecentAsync(10)).Single();

        Assert.That(queuedJob.JobType, Is.EqualTo(BackgroundJobTypes.SnapshotIngest));
        Assert.That(queuedJob.Status, Is.EqualTo(BackgroundJobStatuses.Pending));

        var payload = JsonSerializer.Deserialize<SnapshotIngestJobPayload>(queuedJob.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.ItsmSource, Is.EqualTo("servicenow-prod"));
        Assert.That(payload.SnapshotDate, Is.EqualTo(new DateOnly(2026, 03, 02)));

        var artifacts = scope.ServiceProvider.GetRequiredService<IArtifactStorage>();
        Assert.That(artifacts.Exists(payload.ArtifactPath), Is.True);
    }

    [Test]
    public async Task PollOnceAsync_leaves_recent_files_in_place_until_they_age_past_the_stability_window()
    {
        var filePath = CreateStableFile("snapshot-2026-03-02.csv", "number,company,state\nPRB0002,Contoso,Open\n", adjustLastWrite: false);

        var service = CreatePoller(new FileSharePollingOptions
        {
            Enabled = true,
            PollIntervalSeconds = 1,
            WatchedPath = _watchPath,
            FilePatterns = ["*.csv"],
            ArchivePath = _archivePath,
            ErrorPath = _errorPath,
            MaxFilesPerCycle = 10,
            ItsmSource = "servicenow-prod",
            StableFileAgeSeconds = 60
        });

        var firstCycle = await service.PollOnceAsync(CancellationToken.None);
        Assert.That(firstCycle.QueuedCount, Is.EqualTo(0));
        Assert.That(File.Exists(filePath), Is.True);
        Assert.That(CountJobs(), Is.EqualTo(0));

        File.SetLastWriteTimeUtc(filePath, DateTime.UtcNow.AddMinutes(-5));

        var secondCycle = await service.PollOnceAsync(CancellationToken.None);
        Assert.That(secondCycle.QueuedCount, Is.EqualTo(1));
        Assert.That(CountJobs(), Is.EqualTo(1));
    }

    private FileSharePollingService CreatePoller(FileSharePollingOptions options)
    {
        return new FileSharePollingService(
            _services,
            Options.Create(options),
            NullLogger<FileSharePollingService>.Instance);
    }

    private string CreateStableFile(string fileName, string content, bool adjustLastWrite = true)
    {
        string path = Path.Combine(_watchPath, fileName);
        File.WriteAllText(path, content);
        if (adjustLastWrite)
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-5));
        }

        return path;
    }

    private int CountJobs()
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UpmsDbContext>();
        return db.BackgroundJobs.Count();
    }
}
