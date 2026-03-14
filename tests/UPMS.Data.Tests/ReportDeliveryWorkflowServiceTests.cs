namespace UPMS.Data.Tests;

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UPMS.Data.Artifacts;
using UPMS.Data.Delivery;
using UPMS.Data.Jobs;
using UPMS.Reporting.Delivery;

[TestFixture]
public sealed class ReportDeliveryWorkflowServiceTests
{
    private static UpmsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UpmsDbContext>()
            .UseInMemoryDatabase($"report-delivery-{Guid.NewGuid():N}")
            .Options;

        return new UpmsDbContext(options);
    }

    [Test]
    public async Task QueueAsync_creates_delivery_records_and_background_jobs_for_multiple_lists()
    {
        await using var context = CreateContext();
        var distributionLists = new DistributionListService(context);
        var jobs = new BackgroundJobService(context);
        var artifacts = new TestArtifactStorage();
        var sender = new RecordingSender();
        var workflow = new ReportDeliveryWorkflowService(context, jobs, artifacts, sender, NullLogger<ReportDeliveryWorkflowService>.Instance);

        var companyListA = await distributionLists.CreateAsync(
            new SaveDistributionListCommand(
                "Contoso",
                "Leadership",
                null,
                true,
                [new DistributionListRecipientInput("email", "leader@example.com", null, null, true, 0)]),
            "tester");
        var companyListB = await distributionLists.CreateAsync(
            new SaveDistributionListCommand(
                "Contoso",
                "Operations",
                null,
                true,
                [new DistributionListRecipientInput("email", "ops@example.com", null, null, true, 0)]),
            "tester");

        await artifacts.SaveBytesAsync("reports", "status-report.txt", Encoding.UTF8.GetBytes("hello"), "text/plain");
        var reportJob = await jobs.EnqueueAsync(BackgroundJobTypes.ReportExecution, "{}", "tester");

        var deliveries = await workflow.QueueAsync(
            reportJob,
            [companyListA.Id, companyListB.Id],
            "Contoso",
            "reports/status-report.txt",
            "status-report.txt",
            "text/plain",
            "tester");

        Assert.That(deliveries, Has.Count.EqualTo(2));
        Assert.That(deliveries.All(delivery => delivery.Status == ReportDeliveryStatuses.Queued), Is.True);
        Assert.That(await context.ReportDeliveries.CountAsync(), Is.EqualTo(2));
        Assert.That(await context.BackgroundJobs.CountAsync(job => job.JobType == BackgroundJobTypes.ReportDelivery), Is.EqualTo(2));
    }

    [Test]
    public async Task ProcessAsync_marks_delivery_succeeded_when_sender_completes()
    {
        await using var context = CreateContext();
        var distributionLists = new DistributionListService(context);
        var jobs = new BackgroundJobService(context);
        var artifacts = new TestArtifactStorage();
        var sender = new RecordingSender();
        var workflow = new ReportDeliveryWorkflowService(context, jobs, artifacts, sender, NullLogger<ReportDeliveryWorkflowService>.Instance);

        var list = await distributionLists.CreateAsync(
            new SaveDistributionListCommand(
                "Contoso",
                "Leadership",
                null,
                true,
                [new DistributionListRecipientInput("email", "leader@example.com", null, null, true, 0)]),
            "tester");

        await artifacts.SaveBytesAsync("reports", "status-report.html", Encoding.UTF8.GetBytes("<p>hello</p>"), "text/html; charset=utf-8");
        var reportJob = await jobs.EnqueueAsync(BackgroundJobTypes.ReportExecution, "{}", "tester");
        var queued = await workflow.QueueAsync(
            reportJob,
            [list.Id],
            "Contoso",
            "reports/status-report.html",
            "status-report.html",
            "text/html; charset=utf-8",
            "tester");

        var delivery = queued.Single();
        var result = await workflow.ProcessAsync(delivery.Id, Guid.NewGuid());
        var reloaded = await workflow.GetByIdAsync(delivery.Id);

        Assert.That(result.Success, Is.True);
        Assert.That(reloaded, Is.Not.Null);
        Assert.That(reloaded!.Status, Is.EqualTo(ReportDeliveryStatuses.Succeeded));
        Assert.That(reloaded.AttemptCount, Is.EqualTo(1));
        Assert.That(sender.Messages, Has.Count.EqualTo(1));
        Assert.That(sender.Messages[0].To.Select(row => row.Address).ToArray(), Is.EqualTo(new[] { "leader@example.com" }));
    }

    [Test]
    public async Task ProcessAsync_marks_delivery_failed_and_retry_requeues_without_rerender()
    {
        await using var context = CreateContext();
        var distributionLists = new DistributionListService(context);
        var jobs = new BackgroundJobService(context);
        var artifacts = new TestArtifactStorage();
        var sender = new ThrowingSender("smtp failed");
        var workflow = new ReportDeliveryWorkflowService(context, jobs, artifacts, sender, NullLogger<ReportDeliveryWorkflowService>.Instance);

        var list = await distributionLists.CreateAsync(
            new SaveDistributionListCommand(
                "Contoso",
                "Leadership",
                null,
                true,
                [new DistributionListRecipientInput("email", "leader@example.com", null, null, true, 0)]),
            "tester");

        await artifacts.SaveBytesAsync("reports", "status-report.txt", Encoding.UTF8.GetBytes("hello"), "text/plain");
        var reportJob = await jobs.EnqueueAsync(BackgroundJobTypes.ReportExecution, "{}", "tester");
        var queued = await workflow.QueueAsync(
            reportJob,
            [list.Id],
            "Contoso",
            "reports/status-report.txt",
            "status-report.txt",
            "text/plain",
            "tester");

        var delivery = queued.Single();
        var failed = await workflow.ProcessAsync(delivery.Id, Guid.NewGuid());
        var failedDelivery = await workflow.GetByIdAsync(delivery.Id);

        Assert.That(failed.Success, Is.False);
        Assert.That(failedDelivery, Is.Not.Null);
        Assert.That(failedDelivery!.Status, Is.EqualTo(ReportDeliveryStatuses.Failed));
        Assert.That(failedDelivery.LastErrorMessage, Does.Contain("smtp failed"));
        Assert.That(failedDelivery.AttemptCount, Is.EqualTo(1));

        var retried = await workflow.RetryAsync(delivery.Id, "retry-user");

        Assert.That(retried.Status, Is.EqualTo(ReportDeliveryStatuses.Queued));
        Assert.That(retried.AttemptCount, Is.EqualTo(1));
        Assert.That(retried.LastBackgroundJobId, Is.Not.Null);
        Assert.That(await context.BackgroundJobs.CountAsync(job => job.JobType == BackgroundJobTypes.ReportDelivery), Is.EqualTo(2));
    }

    private sealed class RecordingSender : IEmailReportDeliverySender
    {
        public List<EmailDeliveryMessage> Messages { get; } = new();

        public Task SendAsync(EmailDeliveryMessage message, CancellationToken ct = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSender : IEmailReportDeliverySender
    {
        private readonly string _message;

        public ThrowingSender(string message)
        {
            _message = message;
        }

        public Task SendAsync(EmailDeliveryMessage message, CancellationToken ct = default)
        {
            throw new InvalidOperationException(_message);
        }
    }

    private sealed class TestArtifactStorage : IArtifactStorage
    {
        private readonly Dictionary<string, (byte[] Content, string? ContentType)> _files = new(StringComparer.OrdinalIgnoreCase);

        public Task<StoredArtifact> SaveAsync(string category, string fileName, Stream content, string? contentType, CancellationToken ct = default)
        {
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            return SaveBytesAsync(category, fileName, buffer.ToArray(), contentType, ct);
        }

        public Task<StoredArtifact> SaveBytesAsync(string category, string fileName, byte[] content, string? contentType, CancellationToken ct = default)
        {
            var relativePath = $"{category.TrimEnd('/')}/{fileName}";
            _files[relativePath] = (content, contentType);
            return Task.FromResult(new StoredArtifact(relativePath, $"/tmp/{relativePath}", fileName, contentType, content.LongLength));
        }

        public bool Exists(string relativePath) => _files.ContainsKey(relativePath);

        public string GetAbsolutePath(string relativePath) => $"/tmp/{relativePath}";

        public Stream OpenRead(string relativePath)
        {
            var file = _files[relativePath];
            return new MemoryStream(file.Content, writable: false);
        }
    }
}
