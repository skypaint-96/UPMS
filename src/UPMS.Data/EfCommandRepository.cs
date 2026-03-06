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

        // FieldChange is append-only: we never update an existing row.
        // Clone into a fresh entity so EF Core will always generate an INSERT.
        var insert = CloneForInsert(fieldChange);
        _context.FieldChanges.Add(insert);
        await _context.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task RecordFieldChangesAsync(IEnumerable<FieldChange> changes, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var list = changes.ToList();
        if (list.Count == 0)
            return;

        // FieldChange is append-only: we never update an existing row.
        // Clone into fresh entities so EF Core will always generate INSERTs.
        var inserts = list.Select(CloneForInsert).ToList();
        _context.FieldChanges.AddRange(inserts);
        await _context.SaveChangesAsync(ct);
    }

    private static FieldChange CloneForInsert(FieldChange source)
    {
        // Do NOT copy Id. The DB generates it.
        return new FieldChange
        {
            CompanyName = source.CompanyName?.Trim() ?? string.Empty,
            TicketKey = source.TicketKey?.Trim() ?? string.Empty,
            FieldName = source.FieldName?.Trim() ?? string.Empty,
            FieldValue = source.FieldValue,
            ObservedAt = source.ObservedAt,
            SnapshotId = source.SnapshotId,
        };
    }
}
