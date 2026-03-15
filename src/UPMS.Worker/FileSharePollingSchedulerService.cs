namespace UPMS.Worker;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UPMS.Data;

public sealed class FileSharePollingSchedulerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<FileSharePollingSchedulerService> _logger;
    private readonly FileSharePollingOptions _options;

    public FileSharePollingSchedulerService(
        IServiceProvider services,
        IOptions<FileSharePollingOptions> options,
        ILogger<FileSharePollingSchedulerService> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new FileSharePollingOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("File share polling scheduler is disabled.");
            return;
        }

        _logger.LogInformation("File share polling scheduler is enabled.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var sources = scope.ServiceProvider.GetRequiredService<IFileSharePollingSourceService>();
                var dueSources = await sources.GetDueAsync(DateTime.UtcNow, 100, stoppingToken);

                foreach (var source in dueSources)
                {
                    try
                    {
                        var enqueueResult = await sources.QueuePollJobAsync(
                            source.Id,
                            triggeredManually: false,
                            requestedBy: BuildRequestedBy(),
                            stoppingToken);

                        if (!enqueueResult.AlreadyQueued)
                        {
                            _logger.LogInformation(
                                "Queued file share poll job {JobId} for source {SourceId} ({SourceName}).",
                                enqueueResult.Job.Id,
                                source.Id,
                                source.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to queue due file share polling source {SourceId} ({SourceName}).", source.Id, source.Name);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled file share polling scheduler error.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(_options.SchedulerIntervalSeconds, 1)), stoppingToken);
        }
    }

    private static string BuildRequestedBy()
        => $"file-share-poll-scheduler@{Environment.MachineName}";
}
