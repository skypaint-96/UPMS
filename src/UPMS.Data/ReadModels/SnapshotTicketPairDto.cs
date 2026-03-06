namespace UPMS.Data.ReadModels;

/// <summary>
/// Keyless DTO representing a (snapshot_id, ticket_key) pair returned by stored procedures.
/// Matches the properties expected by the EF Core model snapshot.
/// </summary>
public class SnapshotTicketPairDto
{
    public Guid SnapshotId { get; set; }
    public string TicketKey { get; set; } = string.Empty;
}
