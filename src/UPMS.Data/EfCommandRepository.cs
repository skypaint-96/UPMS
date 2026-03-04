namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core–backed implementation of <see cref="ICommandRepository"/>.
/// </summary>
public class EfCommandRepository : ICommandRepository
{
    private readonly UpmsDbContext _context;

    public EfCommandRepository(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // ── Snapshots ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<Snapshot> CreateSnapshotAsync(Snapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _context.Snapshots.Add(snapshot);
        await _context.SaveChangesAsync(ct);
        return snapshot;
    }

    // ── Snapshot tickets ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task AddSnapshotTicketAsync(SnapshotTicket ticket, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return AddSnapshotTicketsAsync([ticket], ct);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Uses a single bulk <c>INSERT … unnest(…) ON CONFLICT (snapshot_id, ticket_key) DO NOTHING</c>
    /// so that duplicate rows are silently ignored and the entire batch is sent in one round-trip.
    /// </remarks>
    public async Task AddSnapshotTicketsAsync(IEnumerable<SnapshotTicket> tickets, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tickets);

        var list = tickets.ToList();
        if (list.Count == 0)
            return;

        var ids          = list.Select(t => t.Id).ToArray();
        var snapshotIds  = list.Select(t => t.SnapshotId).ToArray();
        var companies    = list.Select(t => t.CompanyName).ToArray();
        var ticketKeys   = list.Select(t => t.TicketKey).ToArray();

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO snapshot_ticket (id, snapshot_id, company_name, ticket_key)
            SELECT * FROM unnest({ids}::uuid[], {snapshotIds}::uuid[], {companies}::varchar[], {ticketKeys}::varchar[])
                AS t(id, snapshot_id, company_name, ticket_key)
            ON CONFLICT (snapshot_id, ticket_key) DO NOTHING
            """, ct);
    }

    // ── Field changes ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task RecordFieldChangeAsync(FieldChange fieldChange, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(fieldChange);

        _context.FieldChanges.Add(fieldChange);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task RecordFieldChangesAsync(IEnumerable<FieldChange> changes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var list = changes.ToList();
        if (list.Count == 0)
            return;

        _context.FieldChanges.AddRange(list);
        await _context.SaveChangesAsync(ct);
    }
}
