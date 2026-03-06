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

    /// <inheritdoc/>
    public async Task<Snapshot> CreateSnapshotAsync(Snapshot snapshot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _context.Snapshots.Add(snapshot);
        await _context.SaveChangesAsync(ct);
        return snapshot;
    }

    /// <inheritdoc/>
    public Task AddSnapshotTicketAsync(SnapshotTicket ticket, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return AddSnapshotTicketsAsync([ticket], ct);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// EF Core doesn't natively support Postgres <c>ON CONFLICT DO NOTHING</c> inserts.
    /// To keep behavior equivalent to the previous raw SQL implementation, we de-duplicate
    /// rows in-memory and then insert in one transaction.
    /// </remarks>
    public async Task AddSnapshotTicketsAsync(IEnumerable<SnapshotTicket> tickets, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tickets);

        var list = tickets
            .Where(t => t.SnapshotId != Guid.Empty && !string.IsNullOrWhiteSpace(t.TicketKey))
            .Select(t =>
            {
                t.TicketKey = t.TicketKey.Trim();
                t.CompanyName = t.CompanyName?.Trim() ?? string.Empty;
                return t;
            })
            .GroupBy(t => new { t.SnapshotId, t.TicketKey })
            .Select(g => g.First())
            .ToList();

        if (list.Count == 0)
            return;

        _context.SnapshotTickets.AddRange(list);
        await _context.SaveChangesAsync(ct);
    }

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
