namespace UPMS.Data;

/// <summary>
/// Represents a raw data snapshot from an ITSM source.
/// </summary>
public class Snapshot
{
    public required Guid Id { get; init; }
    public required string ItsmSource { get; init; }
    public required DateTime SnapshotDate { get; init; }
    public required string UploadedBy { get; init; }
    public required DateTime UploadedAt { get; init; }
    public string? UploadMetadata { get; init; }
}
