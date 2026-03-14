namespace UPMS.Data.Jobs;

public interface ISnapshotIngestJobSubmissionService
{
    Task<BackgroundJob> QueueAsync(
        Stream content,
        string fileName,
        string? contentType,
        string itsmSource,
        DateOnly snapshotDate,
        string? requestedBy,
        CancellationToken ct = default);
}
