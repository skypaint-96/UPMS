namespace UPMS.Data.Tests;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UPMS.Data.Jobs;

[TestFixture]
public sealed class FileSharePollingSourceServiceTests
{
    private UpmsDbContext _context = null!;
    private FileSharePollingSourceService _service = null!;
    private string _allowedRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _allowedRoot = Path.Combine(Path.GetTempPath(), $"upms-file-source-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_allowedRoot);

        var options = new DbContextOptionsBuilder<UpmsDbContext>()
            .UseInMemoryDatabase($"upms-file-source-db-{Guid.NewGuid():N}")
            .Options;

        _context = new UpmsDbContext(options);
        _context.ItsmSources.Add(new ItsmSource
        {
            Id = 1,
            Name = "servicenow-prod",
            DisplayLabel = "ServiceNow Prod"
        });
        _context.SaveChanges();

        _service = new FileSharePollingSourceService(
            _context,
            Options.Create(new FileSharePollingOptions
            {
                Enabled = true,
                AllowUserManagedSources = true,
                DefaultPollIntervalSeconds = 300,
                MinPollIntervalSeconds = 60,
                MaxPollIntervalSeconds = 3600,
                DefaultStableFileAgeSeconds = 30,
                MaxFilesPerCycleCap = 100,
                AllowedWatchedRoots = [_allowedRoot],
                AllowedArchiveRoots = [_allowedRoot],
                AllowedErrorRoots = [_allowedRoot],
            }));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();

        if (Directory.Exists(_allowedRoot))
        {
            try
            {
                Directory.Delete(_allowedRoot, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temporary file-share source test roots.
            }
        }
    }

    [Test]
    public void CreateAsync_rejects_paths_outside_allowed_roots()
    {
        var request = new FileSharePollingSourceUpsert(
            "Out of bounds",
            true,
            Path.Combine(Path.GetTempPath(), "elsewhere-watch"),
            new[] { "*.csv" },
            Path.Combine(_allowedRoot, "archive"),
            Path.Combine(_allowedRoot, "error"),
            "servicenow-prod",
            300,
            10,
            30);

        Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(request, "tester", CancellationToken.None));
    }

    [Test]
    public async Task QueuePollJobAsync_returns_existing_pending_job_when_source_is_already_active()
    {
        var source = await _service.CreateAsync(
            new FileSharePollingSourceUpsert(
                "Nightly drop",
                true,
                Path.Combine(_allowedRoot, "watch"),
                new[] { "*.csv" },
                Path.Combine(_allowedRoot, "archive"),
                Path.Combine(_allowedRoot, "error"),
                "servicenow-prod",
                300,
                10,
                30),
            "tester",
            CancellationToken.None);

        var first = await _service.QueuePollJobAsync(source.Id, triggeredManually: true, requestedBy: "tester", CancellationToken.None);
        var second = await _service.QueuePollJobAsync(source.Id, triggeredManually: true, requestedBy: "tester", CancellationToken.None);

        Assert.That(first.AlreadyQueued, Is.False);
        Assert.That(first.Job.JobType, Is.EqualTo(BackgroundJobTypes.FileSharePoll));
        Assert.That(second.AlreadyQueued, Is.True);
        Assert.That(second.Job.Id, Is.EqualTo(first.Job.Id));
        Assert.That(_context.BackgroundJobs.Count(), Is.EqualTo(1));
    }
}
