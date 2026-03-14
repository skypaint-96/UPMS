namespace UPMS.Worker;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using UPMS.Data;
using UPMS.Data.Artifacts;
using UPMS.Data.Jobs;
using UPMS.Ingestion;
using UPMS.Reporting;
using UPMS.Reporting.Delivery;
using UPMS.Reporting.Plugins;

public sealed class BackgroundJobWorker : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IServiceProvider _services;
    private readonly ILogger<BackgroundJobWorker> _logger;
    private readonly WorkerOptions _options;

    public BackgroundJobWorker(
        IServiceProvider services,
        IOptions<WorkerOptions> options,
        ILogger<BackgroundJobWorker> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new WorkerOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var supportedTypes = new[]
        {
            BackgroundJobTypes.SnapshotIngest,
            BackgroundJobTypes.ReportExecution,
            BackgroundJobTypes.ReportDelivery
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var jobs = scope.ServiceProvider.GetRequiredService<IBackgroundJobService>();

                var job = await jobs.TryClaimNextAsync(
                    Environment.MachineName,
                    supportedTypes,
                    TimeSpan.FromSeconds(Math.Max(_options.LeaseDurationSeconds, 30)),
                    stoppingToken);

                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(_options.PollIntervalSeconds, 1)), stoppingToken);
                    continue;
                }

                _logger.LogInformation("Claimed background job {JobId} ({JobType}).", job.Id, job.JobType);
                await ProcessJobAsync(job, scope.ServiceProvider, jobs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled worker loop error.");
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(_options.PollIntervalSeconds, 1)), stoppingToken);
            }
        }
    }

    private async Task ProcessJobAsync(
        BackgroundJob job,
        IServiceProvider services,
        IBackgroundJobService jobs,
        CancellationToken ct)
    {
        try
        {
            switch (job.JobType)
            {
                case BackgroundJobTypes.SnapshotIngest:
                    await ProcessSnapshotIngestAsync(job, services, jobs, ct);
                    break;

                case BackgroundJobTypes.ReportExecution:
                    await ProcessReportExecutionAsync(job, services, jobs, ct);
                    break;

                case BackgroundJobTypes.ReportDelivery:
                    await ProcessReportDeliveryAsync(job, services, jobs, ct);
                    break;

                default:
                    await jobs.MarkFailedAsync(job.Id, $"Unsupported job type '{job.JobType}'.", ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background job {JobId} failed.", job.Id);
            await jobs.MarkFailedAsync(job.Id, ex.Message, ct);
        }
    }

    private async Task ProcessSnapshotIngestAsync(
        BackgroundJob job,
        IServiceProvider services,
        IBackgroundJobService jobs,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<SnapshotIngestJobPayload>(job.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Snapshot ingest payload was empty or invalid.");

        var artifacts = services.GetRequiredService<IArtifactStorage>();
        var ingestService = services.GetRequiredService<ISnapshotIngestService>();

        if (!artifacts.Exists(payload.ArtifactPath))
            throw new FileNotFoundException("Queued upload artifact could not be found.", payload.ArtifactPath);

        using var stream = artifacts.OpenRead(payload.ArtifactPath);
        var result = payload.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || payload.OriginalFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? await ingestService.IngestJsonAsync(stream, payload.ItsmSource, payload.SnapshotDate, ct)
            : await ingestService.IngestCsvAsync(stream, payload.ItsmSource, payload.SnapshotDate, ct);

        var resultJson = JsonSerializer.Serialize(result, JsonOptions);

        if (!result.Success)
        {
            await jobs.MarkFailedAsync(job.Id, result.ErrorMessage ?? "Snapshot ingest failed.", ct);
            return;
        }

        await jobs.MarkSucceededAsync(job.Id, resultJson, null, null, null, ct);
    }

    private async Task ProcessReportExecutionAsync(
        BackgroundJob job,
        IServiceProvider services,
        IBackgroundJobService jobs,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ReportExecutionJobPayload>(job.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Report execution payload was empty or invalid.");

        var reporting = services.GetRequiredService<IReportExecutionService>();
        var artifacts = services.GetRequiredService<IArtifactStorage>();
        var deliveryWorkflow = services.GetRequiredService<IReportDeliveryWorkflowService>();

        var result = await reporting.ExecuteAsync(
            payload.PluginId,
            payload.Parameters,
            payload.RequestedBy,
            ct);

        if (!result.Success)
        {
            await jobs.MarkFailedAsync(job.Id, result.ErrorMessage ?? $"Report '{payload.PluginId}' failed.", ct);
            return;
        }

        string? outputFilePath = null;
        string? outputFileName = null;
        string? outputContentType = result.ContentType;

        if (result.OutputType == ReportOutputType.FileDownload && result.FileContent is not null)
        {
            var fileName = string.IsNullOrWhiteSpace(result.FileName)
                ? $"{payload.PluginId}-{job.Id:N}.bin"
                : result.FileName;

            var stored = await artifacts.SaveBytesAsync(
                "reports",
                fileName,
                result.FileContent,
                result.ContentType,
                ct);

            outputFilePath = stored.RelativePath;
            outputFileName = stored.FileName;
            outputContentType = stored.ContentType;
        }
        else if (payload.DistributionListIds is { Count: > 0 }
            && TryCreateArtifactFromPreview(payload.PluginId, job.Id, result, out var previewFileName, out var previewContentType, out var previewBytes))
        {
            var stored = await artifacts.SaveBytesAsync(
                "reports",
                previewFileName,
                previewBytes,
                previewContentType,
                ct);

            outputFilePath = stored.RelativePath;
            outputFileName = stored.FileName;
            outputContentType = stored.ContentType;
        }

        IReadOnlyList<Guid>? deliveryIds = null;

        if (payload.DistributionListIds is { Count: > 0 })
        {
            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                await jobs.MarkFailedAsync(
                    job.Id,
                    "The report rendered successfully but did not produce a deliverable artifact for the selected distribution lists.",
                    ct);
                return;
            }

            if (!payload.Parameters.TryGetValue("company", out var companyName) || string.IsNullOrWhiteSpace(companyName))
            {
                await jobs.MarkFailedAsync(
                    job.Id,
                    "The report rendered successfully but no company parameter was available for delivery orchestration.",
                    ct);
                return;
            }

            var deliveries = await deliveryWorkflow.QueueAsync(
                job,
                payload.DistributionListIds,
                companyName,
                outputFilePath,
                outputFileName,
                outputContentType,
                payload.RequestedBy,
                ct);

            deliveryIds = deliveries.Select(delivery => delivery.Id).ToArray();
        }

        var preview = new ReportExecutionPreviewResult(
            result.OutputType.ToString(),
            result.HtmlContent,
            result.ErrorMessage,
            deliveryIds);

        await jobs.MarkSucceededAsync(
            job.Id,
            JsonSerializer.Serialize(preview, JsonOptions),
            outputFilePath,
            outputFileName,
            outputContentType,
            ct);
    }

    private async Task ProcessReportDeliveryAsync(
        BackgroundJob job,
        IServiceProvider services,
        IBackgroundJobService jobs,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ReportDeliveryJobPayload>(job.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Report delivery payload was empty or invalid.");

        var deliveryWorkflow = services.GetRequiredService<IReportDeliveryWorkflowService>();
        var result = await deliveryWorkflow.ProcessAsync(payload.DeliveryId, job.Id, ct);

        if (!result.Success)
        {
            await jobs.MarkFailedAsync(job.Id, result.ErrorMessage ?? "Report delivery failed.", ct);
            return;
        }

        await jobs.MarkSucceededAsync(
            job.Id,
            JsonSerializer.Serialize(new
            {
                deliveryId = payload.DeliveryId,
                recipientCount = result.RecipientCount
            }, JsonOptions),
            null,
            null,
            null,
            ct);
    }

    private static bool TryCreateArtifactFromPreview(
        string pluginId,
        Guid jobId,
        ReportResult result,
        out string fileName,
        out string contentType,
        out byte[] content)
    {
        fileName = $"{pluginId}-{jobId:N}.bin";
        contentType = result.ContentType ?? "application/octet-stream";
        content = Array.Empty<byte>();

        if (string.IsNullOrWhiteSpace(result.HtmlContent))
            return false;

        if (result.OutputType == ReportOutputType.HtmlContent)
        {
            fileName = $"{pluginId}-{jobId:N}.html";
            contentType = string.IsNullOrWhiteSpace(result.ContentType)
                ? "text/html; charset=utf-8"
                : result.ContentType!;
            content = Encoding.UTF8.GetBytes(result.HtmlContent);
            return true;
        }

        if (result.OutputType == ReportOutputType.PlainText)
        {
            fileName = $"{pluginId}-{jobId:N}.txt";
            contentType = string.IsNullOrWhiteSpace(result.ContentType)
                ? "text/plain; charset=utf-8"
                : result.ContentType!;
            content = Encoding.UTF8.GetBytes(result.HtmlContent);
            return true;
        }

        return false;
    }
}

public sealed class WorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 5;
    public int LeaseDurationSeconds { get; set; } = 120;
}
