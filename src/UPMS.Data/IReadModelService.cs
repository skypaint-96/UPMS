namespace UPMS.Data;

using UPMS.Data.ReadModels;

/// <summary>
/// Read-side service interface backed by stored procedures via EF Core.
/// </summary>
public interface IReadModelService
{
    /// <summary>
    /// Returns all field name/value pairs for a ticket as they were at <paramref name="asOfTime"/>.
    /// Backed by <c>get_ticket_at_time</c>.
    /// </summary>
    Task<IReadOnlyList<TicketFieldAtTimeDto>> GetTicketFieldsAtTimeAsync(
        string companyName, string ticketKey, DateTimeOffset asOfTime, CancellationToken ct = default);

    /// <summary>
    /// Same as <see cref="GetTicketFieldsAtTimeAsync"/> but also returns the originating snapshot ID.
    /// Backed by <c>get_ticket_at_time_with_metadata</c>.
    /// </summary>
    Task<IReadOnlyList<TicketFieldWithMetadataDto>> GetTicketFieldsAtTimeWithMetadataAsync(
        string companyName, string ticketKey, DateTimeOffset asOfTime, CancellationToken ct = default);

    /// <summary>
    /// Returns all ticket keys present in the given snapshot.
    /// Backed by <c>get_tickets_for_snapshot</c>.
    /// </summary>
    Task<IReadOnlyList<SnapshotTicketKeyDto>> GetTicketsForSnapshotAsync(
        Guid snapshotId, CancellationToken ct = default);

    /// <summary>
    /// Returns a paged subset of ticket keys for the given snapshot.
    /// Backed by <c>get_tickets_for_snapshot_paged</c>.
    /// </summary>
    Task<IReadOnlyList<SnapshotTicketKeyDto>> GetTicketsForSnapshotPagedAsync(
        Guid snapshotId, int pageSize, int offset, CancellationToken ct = default);

    /// <summary>
    /// Returns the total number of tickets in the given snapshot.
    /// Backed by <c>get_snapshot_ticket_count</c>.
    /// </summary>
    Task<long> GetSnapshotTicketCountAsync(Guid snapshotId, CancellationToken ct = default);

    /// <summary>
    /// Reconstructs the current field state for each of the supplied ticket keys as of <paramref name="asOfTime"/>.
    /// Backed by <c>batch_reconstruct_tickets</c>.
    /// </summary>
    Task<IReadOnlyList<ReconstructedFieldDto>> BatchReconstructTicketsAsync(
        string companyName, IEnumerable<string> ticketKeys, DateTimeOffset asOfTime, CancellationToken ct = default);

    /// <summary>
    /// Reconstructs field state for every ticket in the given snapshot.
    /// Backed by <c>batch_reconstruct_for_snapshot</c>.
    /// </summary>
    Task<IReadOnlyList<ReconstructedFieldDto>> BatchReconstructForSnapshotAsync(
        Guid snapshotId, CancellationToken ct = default);
}
