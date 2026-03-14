namespace UPMS.Api.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UPMS.Data;
using UPMS.Data.Artifacts;
using UPMS.Data.Jobs;
using UPMS.Ingestion;

[TestFixture]
public sealed class ApiAutomatedIngestTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

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
    public async Task Automated_snapshot_job_endpoint_persists_metadata_in_job_payload()
    {
        var response = await SubmitAutomatedAsync(
            "2026-03-05",
            "snapshot-2026-03-05.csv",
            "number,company,state\nPRB0004,Contoso,Open\n",
            new Dictionary<string, string>
            {
                ["sourceSystem"] = "servicenow-exporter",
                ["producer"] = "nightly-job",
                ["correlationId"] = "corr-1001",
                ["sourceFileIdentity"] = "snapshot-2026-03-05",
                ["idempotencyKey"] = "idempotency-1001"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));

        var payload = await response.Content.ReadFromJsonAsync<AutomatedIngestSubmissionResponse>();
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Queued, Is.True);
        Assert.That(payload.Job, Is.Not.Null);
        Assert.That(payload.ContentSha256, Is.Not.Null.And.Not.Empty);

        using var scope = _factory.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
        var artifacts = scope.ServiceProvider.GetRequiredService<IArtifactStorage>();
        var job = await jobs.GetByIdAsync(payload.Job!.Id);

        Assert.That(job, Is.Not.Null);

        var jobPayload = JsonSerializer.Deserialize<SnapshotIngestJobPayload>(job!.PayloadJson, JsonOptions);
        Assert.That(jobPayload, Is.Not.Null);
        Assert.That(jobPayload!.Metadata, Is.Not.Null);
        Assert.That(jobPayload.Metadata!.UploadChannel, Is.EqualTo(SnapshotUploadChannels.AutomatedApi));
        Assert.That(jobPayload.Metadata.IsAutomated, Is.True);
        Assert.That(jobPayload.Metadata.SourceSystem, Is.EqualTo("servicenow-exporter"));
        Assert.That(jobPayload.Metadata.Producer, Is.EqualTo("nightly-job"));
        Assert.That(jobPayload.Metadata.CorrelationId, Is.EqualTo("corr-1001"));
        Assert.That(jobPayload.Metadata.SourceFileIdentity, Is.EqualTo("snapshot-2026-03-05"));
        Assert.That(jobPayload.Metadata.IdempotencyKey, Is.EqualTo("idempotency-1001"));
        Assert.That(jobPayload.Metadata.ContentSha256, Is.EqualTo(payload.ContentSha256));
        Assert.That(jobPayload.Metadata.ArtifactPath, Is.EqualTo(jobPayload.ArtifactPath));
        Assert.That(artifacts.Exists(jobPayload.ArtifactPath), Is.True);
    }

    [Test]
    public async Task Automated_snapshot_ingest_processes_csv_with_quoted_fields_and_persists_snapshot_metadata()
    {
        var response = await SubmitAutomatedAsync(
            "2026-03-06",
            "snapshot-2026-03-06.csv",
            "number,company,state\nPRB0005,Contoso,\"Investigating, awaiting update\"\n",
            new Dictionary<string, string>
            {
                ["sourceSystem"] = "servicenow-exporter",
                ["producer"] = "nightly-job",
                ["correlationId"] = "corr-1002",
                ["sourceFileIdentity"] = "snapshot-2026-03-06",
                ["idempotencyKey"] = "idempotency-1002"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        var submission = await response.Content.ReadFromJsonAsync<AutomatedIngestSubmissionResponse>();
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission!.Job, Is.Not.Null);

        var result = await ProcessSnapshotJobAsync(submission.Job!.Id);
        Assert.That(result.Success, Is.True);
        Assert.That(result.DuplicateDetected, Is.False);
        Assert.That(result.SnapshotId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.TicketsIngested, Is.EqualTo(1));
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(1));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UpmsDbContext>();
        var snapshot = await db.Snapshots.AsNoTracking().SingleAsync(row => row.Id == result.SnapshotId);
        var metadata = SnapshotIngestMetadataHelper.Deserialize(snapshot.UploadMetadata);
        var stateChange = await db.FieldChanges.AsNoTracking()
            .SingleAsync(change => change.SnapshotId == result.SnapshotId && change.CanonicalFieldName == "State");

        Assert.That(metadata, Is.Not.Null);
        Assert.That(metadata!.UploadChannel, Is.EqualTo(SnapshotUploadChannels.AutomatedApi));
        Assert.That(metadata.SourceSystem, Is.EqualTo("servicenow-exporter"));
        Assert.That(metadata.Producer, Is.EqualTo("nightly-job"));
        Assert.That(metadata.CorrelationId, Is.EqualTo("corr-1002"));
        Assert.That(metadata.OriginalFileName, Is.EqualTo("snapshot-2026-03-06.csv"));
        Assert.That(metadata.ContentSha256, Is.EqualTo(submission.ContentSha256));
        Assert.That(stateChange.FieldValue, Is.EqualTo("Investigating, awaiting update"));
    }

    [Test]
    public async Task Automated_snapshot_ingest_reports_invalid_csv_without_persisting_snapshot()
    {
        var response = await SubmitAutomatedAsync(
            "2026-03-07",
            "snapshot-2026-03-07.csv",
            "number,company,state\n\"PRB0006,Contoso,Open\n",
            new Dictionary<string, string>
            {
                ["sourceSystem"] = "servicenow-exporter",
                ["producer"] = "nightly-job",
                ["sourceFileIdentity"] = "snapshot-2026-03-07-invalid"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        var submission = await response.Content.ReadFromJsonAsync<AutomatedIngestSubmissionResponse>();
        Assert.That(submission, Is.Not.Null);
        Assert.That(submission!.Job, Is.Not.Null);

        int snapshotCountBefore;
        using (var beforeScope = _factory.Services.CreateScope())
        {
            var db = beforeScope.ServiceProvider.GetRequiredService<UpmsDbContext>();
            snapshotCountBefore = await db.Snapshots.CountAsync(row => row.SnapshotDate == new DateTime(2026, 03, 07, 0, 0, 0, DateTimeKind.Utc));
        }

        var result = await ProcessSnapshotJobAsync(submission.Job!.Id);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Invalid CSV"));

        using var afterScope = _factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<UpmsDbContext>();
        var snapshotCountAfter = await afterDb.Snapshots.CountAsync(row => row.SnapshotDate == new DateTime(2026, 03, 07, 0, 0, 0, DateTimeKind.Utc));
        Assert.That(snapshotCountAfter, Is.EqualTo(snapshotCountBefore));
    }

    [Test]
    public async Task Automated_snapshot_ingest_detects_duplicate_submission_and_does_not_queue_second_job()
    {
        var firstResponse = await SubmitAutomatedAsync(
            "2026-03-08",
            "snapshot-2026-03-08.csv",
            "number,company,state\nPRB0007,Contoso,Resolved\n",
            new Dictionary<string, string>
            {
                ["sourceSystem"] = "servicenow-exporter",
                ["producer"] = "nightly-job",
                ["sourceFileIdentity"] = "snapshot-2026-03-08",
                ["idempotencyKey"] = "idempotency-1008"
            });

        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        var firstSubmission = await firstResponse.Content.ReadFromJsonAsync<AutomatedIngestSubmissionResponse>();
        Assert.That(firstSubmission, Is.Not.Null);
        Assert.That(firstSubmission!.Job, Is.Not.Null);

        var firstResult = await ProcessSnapshotJobAsync(firstSubmission.Job!.Id);
        Assert.That(firstResult.Success, Is.True);

        var duplicateResponse = await SubmitAutomatedAsync(
            "2026-03-08",
            "snapshot-2026-03-08.csv",
            "number,company,state\nPRB0007,Contoso,Resolved\n",
            new Dictionary<string, string>
            {
                ["sourceSystem"] = "servicenow-exporter",
                ["producer"] = "nightly-job",
                ["sourceFileIdentity"] = "snapshot-2026-03-08",
                ["idempotencyKey"] = "idempotency-1008"
            });

        Assert.That(duplicateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var duplicateSubmission = await duplicateResponse.Content.ReadFromJsonAsync<AutomatedIngestSubmissionResponse>();
        Assert.That(duplicateSubmission, Is.Not.Null);
        Assert.That(duplicateSubmission!.Queued, Is.False);
        Assert.That(duplicateSubmission.DuplicateDetected, Is.True);
        Assert.That(duplicateSubmission.DuplicateReason, Is.Not.Null.And.Not.Empty);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UpmsDbContext>();
        var queuedJobs = await db.BackgroundJobs.CountAsync(job => job.JobType == BackgroundJobTypes.SnapshotIngest);
        var snapshotsForDate = await db.Snapshots.CountAsync(snapshot => snapshot.ItsmSource == "servicenow-prod" && snapshot.SnapshotDate == new DateTime(2026, 03, 08, 0, 0, 0, DateTimeKind.Utc));

        Assert.That(queuedJobs, Is.EqualTo(1));
        Assert.That(snapshotsForDate, Is.EqualTo(1));
    }

    private async Task<HttpResponseMessage> SubmitAutomatedAsync(
        string snapshotDate,
        string fileName,
        string fileContent,
        IReadOnlyDictionary<string, string>? extraFields = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("servicenow-prod"), "itsmSource");
        form.Add(new StringContent(snapshotDate), "snapshotDate");

        if (extraFields is not null)
        {
            foreach (var (key, value) in extraFields)
            {
                form.Add(new StringContent(value), key);
            }
        }

        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(fileContent));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("text/csv");
        form.Add(file, "file", fileName);

        return await _client.PostAsync("/api/v1/jobs/snapshot-ingest/automated", form);
    }

    private async Task<IngestResult> ProcessSnapshotJobAsync(Guid jobId)
    {
        using var scope = _factory.Services.CreateScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();
        var processor = scope.ServiceProvider.GetRequiredService<ISnapshotIngestJobProcessor>();

        var job = await jobs.GetByIdAsync(jobId);
        Assert.That(job, Is.Not.Null);

        var payload = JsonSerializer.Deserialize<SnapshotIngestJobPayload>(job!.PayloadJson, JsonOptions);
        Assert.That(payload, Is.Not.Null);

        var result = await processor.ProcessAsync(payload!, CancellationToken.None);
        if (result.Success)
        {
            await jobs.MarkSucceededAsync(job.Id, JsonSerializer.Serialize(result, JsonOptions), null, null, null, CancellationToken.None);
        }
        else
        {
            await jobs.MarkFailedAsync(job.Id, result.ErrorMessage ?? "Snapshot ingest failed.", CancellationToken.None);
        }

        return result;
    }

    private sealed record AutomatedIngestSubmissionResponse(
        bool Queued,
        bool DuplicateDetected,
        Guid? ExistingSnapshotId,
        string? DuplicateReason,
        string? ContentSha256,
        BackgroundJobRow? Job);

    private sealed record BackgroundJobRow(Guid Id, string JobType, string Status);
}
