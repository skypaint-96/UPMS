namespace UPMS.Data.Tests;

using Microsoft.EntityFrameworkCore;
using UPMS.Data.Jobs;

[TestFixture]
public sealed class BackgroundJobServiceTests
{
    private static UpmsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UpmsDbContext>()
            .UseInMemoryDatabase($"background-jobs-{Guid.NewGuid():N}")
            .Options;

        return new UpmsDbContext(options);
    }

    [Test]
    public async Task EnqueueAsync_creates_pending_job()
    {
        await using var context = CreateContext();
        var service = new BackgroundJobService(context);

        var job = await service.EnqueueAsync(BackgroundJobTypes.ReportExecution, "{}", "tester");

        Assert.That(job.Status, Is.EqualTo(BackgroundJobStatuses.Pending));
        Assert.That(job.JobType, Is.EqualTo(BackgroundJobTypes.ReportExecution));
    }

    [Test]
    public async Task TryClaimNextAsync_moves_job_to_running()
    {
        await using var context = CreateContext();
        var service = new BackgroundJobService(context);
        var job = await service.EnqueueAsync(BackgroundJobTypes.SnapshotIngest, "{}", "tester");

        var claimed = await service.TryClaimNextAsync("worker-a", [BackgroundJobTypes.SnapshotIngest], TimeSpan.FromMinutes(1));

        Assert.That(claimed, Is.Not.Null);
        Assert.That(claimed!.Status, Is.EqualTo(BackgroundJobStatuses.Running));
        Assert.That(claimed.LeaseOwner, Is.EqualTo("worker-a"));
    }

    [Test]
    public async Task MarkSucceededAsync_sets_terminal_state_and_output_metadata()
    {
        await using var context = CreateContext();
        var service = new BackgroundJobService(context);
        var job = await service.EnqueueAsync(BackgroundJobTypes.ReportExecution, "{}", "tester");

        await service.MarkSucceededAsync(job.Id, "{\"ok\":true}", "reports/a.txt", "a.txt", "text/plain");
        var loaded = await service.GetByIdAsync(job.Id);

        Assert.That(loaded!.Status, Is.EqualTo(BackgroundJobStatuses.Succeeded));
        Assert.That(loaded.OutputFileName, Is.EqualTo("a.txt"));
    }
}
