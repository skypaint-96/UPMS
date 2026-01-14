namespace UPMS.Api.Models;

/// <summary>
/// Represents an atomic field-level change for a ticket at a point in time.
/// Maps to the field_change table - the source of truth for ticket state reconstruction.
/// </summary>
public class FieldChange
{
    /// <summary>
    /// Primary key (BIGSERIAL in PostgreSQL).
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Multi-tenant identifier (denormalized for query efficiency).
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Business identifier from the source ITSM system (e.g., INC0001234, JIRA-5678).
    /// </summary>
    public string TicketKey { get; set; } = string.Empty;

    /// <summary>
    /// The field being tracked (e.g., 'Status', 'Priority', 'Assignee').
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// The field value as TEXT. NULL represents explicit null/empty values.
    /// </summary>
    public string? FieldValue { get; set; }

    /// <summary>
    /// When this field value was observed (from snapshot_date).
    /// Critical for point-in-time reconstruction.
    /// </summary>
    public DateTimeOffset ObservedAt { get; set; }

    /// <summary>
    /// Link to the snapshot this change came from (for traceability).
    /// </summary>
    public Guid SnapshotId { get; set; }

    /// <summary>
    /// Audit timestamp for record creation.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
