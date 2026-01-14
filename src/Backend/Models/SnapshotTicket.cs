namespace UPMS.Api.Models;

/// <summary>
/// Represents a lightweight index mapping tickets to snapshots.
/// Maps to the snapshot_ticket table.
/// </summary>
public class SnapshotTicket
{
    /// <summary>
    /// Primary key (UUID).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the snapshot.
    /// </summary>
    public Guid SnapshotId { get; set; }

    /// <summary>
    /// Multi-tenant identifier (denormalized for query efficiency).
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Business identifier from the source ITSM system (e.g., INC0001234, JIRA-5678).
    /// </summary>
    public string TicketKey { get; set; } = string.Empty;

    /// <summary>
    /// Audit timestamp for record creation.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
