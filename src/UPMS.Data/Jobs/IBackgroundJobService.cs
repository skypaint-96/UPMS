namespace UPMS.Data.Jobs;

public interface IBackgroundJobService
{
    Task<BackgroundJob> EnqueueAsync(
        string jobType,
        string payloadJson,
        string? requestedBy,
        CancellationToken ct = default);

    Task<BackgroundJob?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<BackgroundJob>> GetRecentAsync(int take = 50, CancellationToken ct = default);

    Task<BackgroundJob?> TryClaimNextAsync(
        string workerName,
        IReadOnlyCollection<string> supportedJobTypes,
        TimeSpan leaseDuration,
        CancellationToken ct = default);

    Task MarkSucceededAsync(
        Guid id,
        string? resultJson,
        string? outputFilePath,
        string? outputFileName,
        string? outputContentType,
        CancellationToken ct = default);

    Task MarkFailedAsync(Guid id, string errorMessage, CancellationToken ct = default);

    Task RenewLeaseAsync(Guid id, string workerName, TimeSpan leaseDuration, CancellationToken ct = default);
}
