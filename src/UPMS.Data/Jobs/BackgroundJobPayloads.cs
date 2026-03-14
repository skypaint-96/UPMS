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
    string? RequestedBy);

public sealed record ReportExecutionPreviewResult(
    string OutputType,
    string? HtmlContent,
    string? ErrorMessage);
