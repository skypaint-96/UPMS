namespace UPMS.Ingestion;

using UPMS.Data.Jobs;

public interface ISnapshotIngestJobProcessor
{
    Task<IngestResult> ProcessAsync(SnapshotIngestJobPayload payload, CancellationToken ct = default);
}
