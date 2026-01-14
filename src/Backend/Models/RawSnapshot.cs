using System.Text.Json;

namespace UPMS.Api.Models;

/// <summary>
/// Represents a snapshot of ITSM data at a point in time.
/// Maps to the raw_snapshot table.
/// </summary>
public class RawSnapshot
{
    /// <summary>
    /// Primary key (UUID).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Multi-tenant identifier. All queries must scope by company.
    /// </summary>
    public Guid CompanyId { get; set; }

    /// <summary>
    /// Source ITSM system identifier (e.g., 'servicenow', 'jira', 'zendesk').
    /// </summary>
    public string ItsmSource { get; set; } = string.Empty;

    /// <summary>
    /// Business date/time this snapshot represents.
    /// </summary>
    public DateTimeOffset SnapshotDate { get; set; }

    /// <summary>
    /// User or service that uploaded the snapshot.
    /// </summary>
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>
    /// When the snapshot was uploaded.
    /// </summary>
    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>
    /// Optional metadata about the upload (filename, row count, validation status, etc.).
    /// Stored as JSONB in PostgreSQL.
    /// </summary>
    public JsonDocument? UploadMetadata { get; set; }

    /// <summary>
    /// Audit timestamp for record creation.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
