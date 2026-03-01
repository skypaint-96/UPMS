namespace UPMS.Data;

/// <summary>
/// Represents a raw data snapshot from an ITSM source (<c>raw_snapshot</c> table).
/// </summary>
public class Snapshot
{
    /// <summary>Parameterless constructor required by EF Core and Dapper.</summary>
    public Snapshot() { }

    public Guid Id { get; set; }
    public string ItsmSource { get; set; } = string.Empty;
    public DateTime SnapshotDate { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string? UploadMetadata { get; set; }
}
