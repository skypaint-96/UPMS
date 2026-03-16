namespace UPMS.Ingestion;

/// <summary>
/// Summary of a completed snapshot ingest operation.
/// </summary>
public class IngestResult
{
    public required bool Success { get; init; }
    public required Guid SnapshotId { get; init; }
    public required int TicketsIngested { get; init; }
    public required int FieldChangesRecorded { get; init; }
    public bool DuplicateDetected { get; init; }
    public Guid? DuplicateOfSnapshotId { get; init; }
    public string? DuplicateReason { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static IngestResult Failure(string errorMessage, IReadOnlyList<string>? warnings = null) => new()
    {
        Success = false,
        SnapshotId = Guid.Empty,
        TicketsIngested = 0,
        FieldChangesRecorded = 0,
        DuplicateDetected = false,
        DuplicateOfSnapshotId = null,
        DuplicateReason = null,
        ErrorMessage = errorMessage,
        Warnings = warnings ?? Array.Empty<string>()
    };

    public static IngestResult Duplicate(Guid snapshotId, string reason, IReadOnlyList<string>? warnings = null) => new()
    {
        Success = true,
        SnapshotId = snapshotId,
        TicketsIngested = 0,
        FieldChangesRecorded = 0,
        DuplicateDetected = true,
        DuplicateOfSnapshotId = snapshotId,
        DuplicateReason = reason,
        ErrorMessage = null,
        Warnings = warnings ?? Array.Empty<string>()
    };
}
