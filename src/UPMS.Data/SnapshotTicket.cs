namespace UPMS.Data;

/// <summary>
/// Represents a single ticket key observed within a snapshot (<c>snapshot_ticket</c> table).
/// </summary>
public class SnapshotTicket
{
    /// <summary>Parameterless constructor required by EF Core and Dapper.</summary>
    public SnapshotTicket() { }

    public Guid Id { get; set; }
    public Guid SnapshotId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string TicketKey { get; set; } = string.Empty;

    // Navigation property for EF Core
    public Snapshot? Snapshot { get; set; }
}
