namespace UPMS.Data;

/// <summary>
/// DTO for snapshot query results from the database.
/// </summary>
internal class SnapshotQueryResult
{
    public Guid Id { get; set; }
    public string ItsmSource { get; set; } = string.Empty;
    public string SnapshotDate { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public string UploadedAt { get; set; } = string.Empty;
    public string? UploadMetadata { get; set; }
}
