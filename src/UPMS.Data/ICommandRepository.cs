namespace UPMS.Data;

/// <summary>
/// Write-side repository interface backed by EF Core.
/// Handles creation of snapshots, snapshot tickets, and field changes.
/// </summary>
public interface ICommandRepository
{
    /// <summary>Persists a new snapshot record and returns the saved entity.</summary>
    Task<Snapshot> CreateSnapshotAsync(Snapshot snapshot, CancellationToken ct = default);

    /// <summary>Inserts a single snapshot ticket, ignoring conflicts on (snapshot_id, ticket_key).</summary>
    Task AddSnapshotTicketAsync(SnapshotTicket ticket, CancellationToken ct = default);

    /// <summary>Bulk-inserts snapshot tickets, ignoring conflicts on (snapshot_id, ticket_key).</summary>
    Task AddSnapshotTicketsAsync(IEnumerable<SnapshotTicket> tickets, CancellationToken ct = default);

    /// <summary>Persists a single field change record.</summary>
    Task RecordFieldChangeAsync(FieldChange fieldChange, CancellationToken ct = default);

    /// <summary>Bulk-persists a collection of field change records.</summary>
    Task RecordFieldChangesAsync(IEnumerable<FieldChange> changes, CancellationToken ct = default);
}
