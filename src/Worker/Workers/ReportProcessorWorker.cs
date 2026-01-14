using UPMS.Worker.Data;

namespace UPMS.Worker.Workers;

public class ReportProcessorWorker : BackgroundService
{
    private readonly ILogger<ReportProcessorWorker> _logger;
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly IConfiguration _configuration;
    private readonly int _pollingIntervalSeconds;

    public ReportProcessorWorker(
        ILogger<ReportProcessorWorker> logger,
        IDbConnectionFactory dbConnectionFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _dbConnectionFactory = dbConnectionFactory;
        _configuration = configuration;
        _pollingIntervalSeconds = _configuration.GetValue<int>("Worker:PollingIntervalSeconds", 30);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Report Processor Worker starting at: {Time} with polling interval: {Interval}s",
            DateTimeOffset.UtcNow,
            _pollingIntervalSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessReportsAsync(stoppingToken);
                
                // Wait for the configured interval before checking again
                await Task.Delay(TimeSpan.FromSeconds(_pollingIntervalSeconds), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Report Processor Worker is stopping due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Report Processor Worker");
            throw;
        }
    }

    private async Task ProcessReportsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Checking for report jobs at: {Time}", DateTimeOffset.UtcNow);

            // Placeholder: This is where we'll poll for pending report jobs
            // Future implementation will:
            // 1. Query database for pending report generation jobs
            // 2. Process each job (generate report, render to file)
            // 3. Upload artifacts to MinIO
            // 4. Update job status in database

            // For now, just log activity for monitoring
            _logger.LogInformation("Report processor heartbeat at: {Time}", DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing reports");
            // Don't throw - allow the worker to continue running
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Report Processor Worker is stopping at: {Time}",
            DateTimeOffset.UtcNow);

        await base.StopAsync(cancellationToken);
    }
}
