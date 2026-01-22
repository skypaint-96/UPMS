namespace UPMS.Data;

/// <summary>
/// DTO for ticket query results from the database.
/// </summary>
internal class TicketQueryResult
{
    public string TicketKey { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ItsmSource { get; set; } = string.Empty;
    public string SnapshotId { get; set; } = string.Empty;
    public string SnapshotDate { get; set; } = string.Empty;
    public string? ObservedAt { get; set; }
}
