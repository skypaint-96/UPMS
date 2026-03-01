namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    /// Uses <c>INSERT … ON CONFLICT (snapshot_id, ticket_key) DO NOTHING</c> so that
    /// duplicate rows (re-ingested snapshots) are silently ignored rather than raising
    /// a unique-constraint violation.
    /// </remarks>
    public async Task AddSnapshotTicketsAsync(IEnumerable<SnapshotTicket> tickets, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tickets);

        var list = tickets.ToList();
        if (list.Count == 0)
            return;

        foreach (var ticket in list)
        {
            await _context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO snapshot_ticket (id, snapshot_id, company_name, ticket_key)
                VALUES (@id, @snapshotId, @companyName, @ticketKey)
                ON CONFLICT (snapshot_id, ticket_key) DO NOTHING
                """,
                [
                    new NpgsqlParameter("id",          ticket.Id),
                    new NpgsqlParameter("snapshotId",  ticket.SnapshotId),
                    new NpgsqlParameter("companyName", ticket.CompanyName),
                    new NpgsqlParameter("ticketKey",   ticket.TicketKey),
                ],
                ct);
        }
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
