namespace UPMS.Data;

/// <summary>
/// Thin wrapper around <see cref="TicketDataService"/> used by scoped plugins.
/// Keeps the public surface minimal and forwards calls to the underlying service.
/// </summary>
public class TicketDataServiceInstance
{
    private readonly TicketDataService _inner;

    public TicketDataServiceInstance(TicketDataService inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public Task<Guid> CreateSnapshotAsync(string itsmSource, DateTime snapshotDate, string uploadedBy = "system", string? uploadMetadata = null)
        => _inner.CreateSnapshotAsync(itsmSource, snapshotDate, uploadedBy, uploadMetadata);

    public Task AddTicketsToSnapshotAsync(Guid snapshotId, IEnumerable<(string TicketKey, string CompanyName)> ticketsWithCompanies)
        => _inner.AddTicketsToSnapshotAsync(snapshotId, ticketsWithCompanies);

    public Task RecordFieldChangeAsync(string companyName, string ticketKey, string fieldName, string? fieldValue, DateTime observedAt, Guid snapshotId)
        => _inner.RecordFieldChangeAsync(companyName, ticketKey, fieldName, fieldValue, observedAt, snapshotId);

    public Task<IEnumerable<Ticket>> GetTicketsAsync(string itsmSource, string companyName, DateTime asOfDate)
        => _inner.GetTicketsAsync(itsmSource, companyName, asOfDate);

    public Task<IEnumerable<Ticket>> GetTicketsFilteredAsync(string itsmSource, DateTime asOfDate, IEnumerable<TicketFieldFilter>? fieldFilters = null)
        => _inner.GetTicketsFilteredAsync(itsmSource, asOfDate, fieldFilters);

    public Task<IEnumerable<Ticket>> GetTicketsBySnapshotAsync(Guid snapshotId)
        => _inner.GetTicketsBySnapshotAsync(snapshotId);

    public Task<IEnumerable<FieldChange>> GetTicketFieldHistoryAsync(string companyName, string ticketKey, string fieldName)
        => _inner.GetTicketFieldHistoryAsync(companyName, ticketKey, fieldName);

    public Task<IEnumerable<FieldChange>> GetTicketHistoryAsync(string companyName, string ticketKey)
        => _inner.GetTicketHistoryAsync(companyName, ticketKey);

    public Task<Snapshot?> GetSnapshotByIdAsync(Guid snapshotId)
        => _inner.GetSnapshotByIdAsync(snapshotId);

    public Task<IEnumerable<Snapshot>> GetSnapshotsAsync(string? itsmSource = null)
        => _inner.GetSnapshotsAsync(itsmSource);

    public Task<IEnumerable<Snapshot>> GetSnapshotsAsync(string? itsmSource, string? companyName)
        => _inner.GetSnapshotsAsync(itsmSource, companyName);
}
