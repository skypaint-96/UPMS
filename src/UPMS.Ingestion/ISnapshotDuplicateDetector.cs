namespace UPMS.Ingestion;

using UPMS.Data;

public interface ISnapshotDuplicateDetector
{
    Task<SnapshotDuplicateMatch?> FindExistingSnapshotAsync(
        string itsmSourceName,
        DateOnly snapshotDate,
        SnapshotIngestMetadata? metadata,
        CancellationToken ct = default);
}

public sealed record SnapshotDuplicateMatch(Guid SnapshotId, string Reason);
