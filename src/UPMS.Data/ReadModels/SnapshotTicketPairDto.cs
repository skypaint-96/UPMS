namespace UPMS.Data.ReadModels;

/// <summary>
/// Keyless DTO pairing a snapshot ID with a ticket key.
/// </summary>
public class SnapshotTicketPairDto
{
    public Guid SnapshotId { get; set; }
    public string TicketKey { get; set; } = string.Empty;
}
