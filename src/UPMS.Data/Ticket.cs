namespace UPMS.Data;

public class Ticket
{
    public required string TicketKey { get; init; }
    public required Guid CompanyId { get; init; }
    public required string ItsmSource { get; init; }
    public required IDictionary<string, string?> Fields { get; init; }
    public required DateTime ObservedAt { get; init; }
    public required Guid SnapshotId { get; init; }
    public required DateTime SnapshotDate { get; init; }
}
