namespace UPMS.Ingestion;

using UPMS.Data;
using UPMS.Data.Artifacts;
using UPMS.Data.Jobs;

public sealed class SnapshotIngestJobProcessor : ISnapshotIngestJobProcessor
{
    private readonly IArtifactStorage _artifacts;
    private readonly ISnapshotIngestService _ingestService;

    public SnapshotIngestJobProcessor(IArtifactStorage artifacts, ISnapshotIngestService ingestService)
    {
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        _ingestService = ingestService ?? throw new ArgumentNullException(nameof(ingestService));
    }

    public async Task<IngestResult> ProcessAsync(SnapshotIngestJobPayload payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!_artifacts.Exists(payload.ArtifactPath))
            throw new FileNotFoundException("Queued upload artifact could not be found.", payload.ArtifactPath);

        var metadata = SnapshotIngestMetadataHelper.Normalize(
            payload.Metadata,
            payload.OriginalFileName,
            payload.ContentType,
            SnapshotUploadChannels.ManualJob,
            automated: payload.Metadata?.IsAutomated ?? false,
            artifactPath: payload.ArtifactPath,
            contentSha256: payload.Metadata?.ContentSha256,
            requestedBy: payload.Metadata?.RequestedBy);

        using var stream = _artifacts.OpenRead(payload.ArtifactPath);
        if (IsJson(payload.OriginalFileName, payload.ContentType))
            return await _ingestService.IngestJsonAsync(stream, payload.ItsmSource, payload.SnapshotDate, metadata, ct);

        return await _ingestService.IngestCsvAsync(stream, payload.ItsmSource, payload.SnapshotDate, metadata, ct);
    }

    private static bool IsJson(string originalFileName, string? contentType)
    {
        if (!string.IsNullOrWhiteSpace(originalFileName)
            && originalFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(contentType)
            && contentType.Contains("json", StringComparison.OrdinalIgnoreCase);
    }
}
