namespace UPMS.Data.Jobs;

public sealed record SnapshotIngestJobPayload(
    string ItsmSource,
    DateOnly SnapshotDate,
    string ArtifactPath,
    string OriginalFileName,
    string ContentType,
    SnapshotIngestMetadata? Metadata = null);

public sealed record ReportExecutionJobPayload(
    string PluginId,
    IReadOnlyDictionary<string, string> Parameters,
    string? RequestedBy,
    IReadOnlyList<Guid>? DistributionListIds);

public sealed record ReportDeliveryJobPayload(Guid DeliveryId);

public sealed record FileSharePollJobPayload(
    Guid SourceId,
    bool TriggeredManually);

public sealed record ReportExecutionPreviewResult(
    string OutputType,
    string? HtmlContent,
    string? ErrorMessage,
    IReadOnlyList<Guid>? DeliveryIds);
