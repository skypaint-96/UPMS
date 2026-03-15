namespace UPMS.Data.Jobs;

public sealed record SnapshotIngestJobPayload(
    string ItsmSource,
    DateOnly SnapshotDate,
    string ArtifactPath,
    string OriginalFileName,
    string ContentType);

public sealed record ReportExecutionJobPayload(
    string PluginId,
    IReadOnlyDictionary<string, string> Parameters,
    string? RequestedBy);

public sealed record FileSharePollJobPayload(
    Guid SourceId,
    bool TriggeredManually);

public sealed record ReportExecutionPreviewResult(
    string OutputType,
    string? HtmlContent,
    string? ErrorMessage);
