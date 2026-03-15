namespace UPMS.Worker;

using System.Text.Json;
using Microsoft.Extensions.Options;
using UPMS.Data;
using UPMS.Data.Artifacts;
using UPMS.Data.Jobs;
using UPMS.Ingestion;
using UPMS.Reporting;
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
    private readonly FileSharePollingOptions _fileSharePollingOptions;

    public BackgroundJobWorker(
        IServiceProvider services,
        IOptions<WorkerOptions> options,
        IOptions<FileSharePollingOptions> fileSharePollingOptions,
        ILogger<BackgroundJobWorker> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new WorkerOptions();
        _fileSharePollingOptions = fileSharePollingOptions?.Value ?? new FileSharePollingOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var supportedTypes = new[]
        {
            BackgroundJobTypes.SnapshotIngest,
            BackgroundJobTypes.ReportExecution,
            BackgroundJobTypes.FileSharePoll
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

                case BackgroundJobTypes.FileSharePoll:
                    await ProcessFileSharePollAsync(job, services, jobs, ct);
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

        var processor = services.GetRequiredService<ISnapshotIngestJobProcessor>();
        var result = await processor.ProcessAsync(payload, ct);

        var resultJson = JsonSerializer.Serialize(result, JsonOptions);

        if (!result.Success)
        {
            await jobs.MarkFailedAsync(job.Id, result.ErrorMessage ?? "Snapshot ingest failed.", ct);
            return;
        }

        await jobs.MarkSucceededAsync(job.Id, resultJson, null, null, null, ct);
    }

    private async Task ProcessFileSharePollAsync(
        BackgroundJob job,
        IServiceProvider services,
        IBackgroundJobService jobs,
        CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<FileSharePollJobPayload>(job.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("File share poll payload was empty or invalid.");

        var sources = services.GetRequiredService<IFileSharePollingSourceService>();

        if (!_fileSharePollingOptions.Enabled)
        {
            var disabledResult = FileSharePollingCycleResult.Disabled("File share polling is disabled by configuration.");
            await jobs.MarkFailedAsync(job.Id, disabledResult.Message ?? "File share polling is disabled by configuration.", ct);
            await sources.MarkRunCompletedAsync(payload.SourceId, job.Id, false, disabledResult.Message, ct);
            return;
        }

        var runner = services.GetRequiredService<IFileSharePollRunner>();

        var source = await sources.GetByIdAsync(payload.SourceId, ct)
            ?? throw new KeyNotFoundException($"File share polling source '{payload.SourceId}' was not found.");

        await sources.MarkRunStartedAsync(payload.SourceId, job.Id, ct);

        if (!payload.TriggeredManually && !source.Enabled)
        {
            var disabledResult = FileSharePollingCycleResult.Disabled("The polling source was disabled before the job started.");
            await jobs.MarkSucceededAsync(job.Id, JsonSerializer.Serialize(disabledResult, JsonOptions), null, null, null, ct);
            await sources.MarkRunCompletedAsync(payload.SourceId, job.Id, true, null, ct);
            return;
        }

        var result = await runner.PollOnceAsync(source, ct);
        if (result.IsFailure)
        {
            await jobs.MarkFailedAsync(job.Id, result.Message ?? $"File share poll failed for source '{source.Name}'.", ct);
            await sources.MarkRunCompletedAsync(payload.SourceId, job.Id, false, result.Message, ct);
            return;
        }

        await jobs.MarkSucceededAsync(job.Id, JsonSerializer.Serialize(result, JsonOptions), null, null, null, ct);
        await sources.MarkRunCompletedAsync(payload.SourceId, job.Id, true, null, ct);
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

            await jobs.MarkSucceededAsync(
                job.Id,
                JsonSerializer.Serialize(new ReportExecutionPreviewResult(result.OutputType.ToString(), null, null), JsonOptions),
                stored.RelativePath,
                stored.FileName,
                stored.ContentType,
                ct);

            return;
        }

        var preview = new ReportExecutionPreviewResult(
            result.OutputType.ToString(),
            result.HtmlContent,
            result.ErrorMessage);

        await jobs.MarkSucceededAsync(
            job.Id,
            JsonSerializer.Serialize(preview, JsonOptions),
            null,
            null,
            result.ContentType,
            ct);
    }
}

public sealed class WorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 5;
    public int LeaseDurationSeconds { get; set; } = 120;
}
