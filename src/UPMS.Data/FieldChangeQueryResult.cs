namespace UPMS.Data;

/// <summary>
/// DTO for field change query results from the database.
/// </summary>
internal class FieldChangeQueryResult
{
    public long Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TicketKey { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
    public string ObservedAt { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
}
