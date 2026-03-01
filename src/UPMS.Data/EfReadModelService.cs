namespace UPMS.Data;

using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UPMS.Data.ReadModels;

/// <summary>
/// EF Core–backed implementation of <see cref="IReadModelService"/>.
/// Stored-procedure result sets are materialised via <c>FromSqlRaw</c> on keyless
/// <see cref="DbSet{T}"/>s. The scalar count procedure falls back to Dapper on the
/// underlying <see cref="System.Data.IDbConnection"/>.
/// </summary>
public class EfReadModelService : IReadModelService
{
    private readonly UpmsDbContext _context;

    public EfReadModelService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // ── get_ticket_at_time ─────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TicketFieldAtTimeDto>> GetTicketFieldsAtTimeAsync(
        Guid companyId, string ticketKey, DateTimeOffset asOfTime, CancellationToken ct = default)
    {
        var results = await _context.TicketFieldAtTimeResults
            .FromSqlRaw(
                "SELECT * FROM get_ticket_at_time(@p_company_id, @p_ticket_key, @p_as_of_time)",
                new NpgsqlParameter("p_company_id",   companyId),
                new NpgsqlParameter("p_ticket_key",   ticketKey),
                new NpgsqlParameter("p_as_of_time",   asOfTime))
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    // ── get_ticket_at_time_with_metadata ───────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TicketFieldWithMetadataDto>> GetTicketFieldsAtTimeWithMetadataAsync(
        Guid companyId, string ticketKey, DateTimeOffset asOfTime, CancellationToken ct = default)
    {
        var results = await _context.TicketFieldWithMetadataResults
            .FromSqlRaw(
                "SELECT * FROM get_ticket_at_time_with_metadata(@p_company_id, @p_ticket_key, @p_as_of_time)",
                new NpgsqlParameter("p_company_id",   companyId),
                new NpgsqlParameter("p_ticket_key",   ticketKey),
                new NpgsqlParameter("p_as_of_time",   asOfTime))
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    // ── get_tickets_for_snapshot ───────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SnapshotTicketKeyDto>> GetTicketsForSnapshotAsync(
        Guid snapshotId, CancellationToken ct = default)
    {
        var results = await _context.SnapshotTicketKeyResults
            .FromSqlRaw(
                "SELECT * FROM get_tickets_for_snapshot(@p_snapshot_id)",
                new NpgsqlParameter("p_snapshot_id", snapshotId))
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    // ── get_tickets_for_snapshot_paged ─────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SnapshotTicketKeyDto>> GetTicketsForSnapshotPagedAsync(
        Guid snapshotId, int pageSize, int offset, CancellationToken ct = default)
    {
        var results = await _context.SnapshotTicketKeyResults
            .FromSqlRaw(
                "SELECT * FROM get_tickets_for_snapshot_paged(@p_snapshot_id, @p_page_size, @p_offset)",
                new NpgsqlParameter("p_snapshot_id", snapshotId),
                new NpgsqlParameter("p_page_size",   pageSize),
                new NpgsqlParameter("p_offset",      offset))
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    // ── get_snapshot_ticket_count ──────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// The stored procedure returns a scalar <c>BIGINT</c> rather than a row set, so
    /// we use Dapper via the underlying ADO.NET connection rather than <c>FromSqlRaw</c>.
    /// </remarks>
    public async Task<long> GetSnapshotTicketCountAsync(Guid snapshotId, CancellationToken ct = default)
    {
        var conn = _context.Database.GetDbConnection();

        if (conn.State != System.Data.ConnectionState.Open)
            await _context.Database.OpenConnectionAsync(ct);

        var result = await conn.ExecuteScalarAsync<long>(
            "SELECT get_snapshot_ticket_count(@p_snapshot_id)",
            new { p_snapshot_id = snapshotId });

        return result;
    }

    // ── batch_reconstruct_tickets ──────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReconstructedFieldDto>> BatchReconstructTicketsAsync(
        Guid companyId, IEnumerable<string> ticketKeys, DateTimeOffset asOfTime, CancellationToken ct = default)
    {
        var results = await _context.ReconstructedFieldResults
            .FromSqlRaw(
                "SELECT * FROM batch_reconstruct_tickets(@p_company_id, @p_ticket_keys, @p_as_of_time)",
                new NpgsqlParameter("p_company_id",   companyId),
                new NpgsqlParameter("p_ticket_keys",  ticketKeys.ToArray()) { DataTypeName = "text[]" },
                new NpgsqlParameter("p_as_of_time",   asOfTime))
            .ToListAsync(ct);

        return results.AsReadOnly();
    }

    // ── batch_reconstruct_for_snapshot ─────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReconstructedFieldDto>> BatchReconstructForSnapshotAsync(
        Guid snapshotId, CancellationToken ct = default)
    {
        var results = await _context.ReconstructedFieldResults
            .FromSqlRaw(
                "SELECT * FROM batch_reconstruct_for_snapshot(@p_snapshot_id)",
                new NpgsqlParameter("p_snapshot_id", snapshotId))
            .ToListAsync(ct);

        return results.AsReadOnly();
    }
}
