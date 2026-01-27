namespace UPMS.Data;

using Microsoft.Extensions.Options;

/// <summary>
/// Instance wrapper for the static TicketDataService for dependency injection.
/// </summary>
public class TicketDataServiceInstance
{
    private readonly IOptions<DatabaseOptions> _options;

    public TicketDataServiceInstance(IOptions<DatabaseOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        // Initialize the static service
        TicketDataService.Initialize(_options);
    }

    /// <summary>
    /// Creates a new snapshot for an ITSM source.
    /// </summary>
    public async Task<Guid> CreateSnapshotAsync(
        string itsmSource,
        DateTime snapshotDate,
        string uploadedBy = "system",
        string? uploadMetadata = null)
    {
        return await TicketDataService.CreateSnapshotAsync(itsmSource, snapshotDate, uploadedBy, uploadMetadata);
    }

    /// <summary>
    /// Adds tickets to a snapshot.
    /// </summary>
    public async Task AddTicketsToSnapshotAsync(
        Guid snapshotId,
        IEnumerable<(string TicketKey, string CompanyName)> ticketsWithCompanies)
    {
        await TicketDataService.AddTicketsToSnapshotAsync(snapshotId, ticketsWithCompanies);
    }

    /// <summary>
    /// Records a field change for a ticket.
    /// </summary>
    public async Task RecordFieldChangeAsync(
        string ticketKey,
        string companyName,
        string fieldName,
        string? fieldValue,
        DateTime observedAt,
        Guid snapshotId)
    {
        await TicketDataService.RecordFieldChangeAsync(ticketKey, companyName, fieldName, fieldValue, observedAt, snapshotId);
    }

    /// <summary>
    /// Gets tickets for a specific ITSM source and company as of a date.
    /// </summary>
    public async Task<IEnumerable<Ticket>> GetTicketsAsync(
        string itsmSource,
        string companyName,
        DateTime asOfDate)
    {
        return await TicketDataService.GetTicketsAsync(itsmSource, companyName, asOfDate);
    }

    /// <summary>
    /// Gets all tickets for a specific snapshot.
    /// </summary>
    public async Task<IEnumerable<Ticket>> GetTicketsBySnapshotAsync(Guid snapshotId)
    {
        return await TicketDataService.GetTicketsBySnapshotAsync(snapshotId);
    }

    /// <summary>
    /// Gets field change history for a ticket.
    /// </summary>
    public async Task<IEnumerable<FieldChange>> GetTicketFieldHistoryAsync(
        string companyName,
        string ticketKey,
        string fieldName)
    {
        return await TicketDataService.GetTicketFieldHistoryAsync(companyName, ticketKey, fieldName);
    }
}
