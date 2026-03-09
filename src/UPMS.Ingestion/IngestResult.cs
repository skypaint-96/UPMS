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
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static IngestResult Failure(string errorMessage) => new()
    {
        Success = false,
        SnapshotId = Guid.Empty,
        TicketsIngested = 0,
        FieldChangesRecorded = 0,
        ErrorMessage = errorMessage
    };
}
