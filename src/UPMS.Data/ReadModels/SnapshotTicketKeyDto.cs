namespace UPMS.Data.ReadModels;

/// <summary>
/// Keyless DTO for rows returned by <c>get_tickets_for_snapshot</c>
/// and <c>get_tickets_for_snapshot_paged</c>.
/// </summary>
public class SnapshotTicketKeyDto
{
    public string TicketKey { get; set; } = string.Empty;
}
