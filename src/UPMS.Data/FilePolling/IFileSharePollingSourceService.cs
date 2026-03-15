namespace UPMS.Data;

using UPMS.Data.Jobs;

public interface IFileSharePollingSourceService
{
    Task<IReadOnlyList<FileSharePollingSource>> GetAllAsync(CancellationToken ct = default);

    Task<FileSharePollingSource?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<FileSharePollingSource>> GetDueAsync(DateTime utcNow, int take = 100, CancellationToken ct = default);

    Task<FileSharePollingSource> CreateAsync(FileSharePollingSourceUpsert request, string? requestedBy, CancellationToken ct = default);

    Task<FileSharePollingSource> UpdateAsync(Guid id, FileSharePollingSourceUpsert request, string? requestedBy, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<FileSharePollingEnqueueResult> QueuePollJobAsync(Guid sourceId, bool triggeredManually, string? requestedBy, CancellationToken ct = default);

    Task MarkRunStartedAsync(Guid sourceId, Guid jobId, CancellationToken ct = default);

    Task MarkRunCompletedAsync(Guid sourceId, Guid jobId, bool succeeded, string? errorMessage, CancellationToken ct = default);
}

public sealed record FileSharePollingSourceUpsert(
    string Name,
    bool Enabled,
    string WatchedPath,
    IReadOnlyList<string>? FilePatterns,
    string ArchivePath,
    string ErrorPath,
    string ItsmSource,
    int? PollIntervalSeconds,
    int? MaxFilesPerCycle,
    int? StableFileAgeSeconds);

public sealed record FileSharePollingEnqueueResult(BackgroundJob Job, bool AlreadyQueued);
