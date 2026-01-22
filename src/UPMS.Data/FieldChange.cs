namespace UPMS.Data;

public class FieldChange
{
    public required long Id { get; init; }
    public required Guid CompanyId { get; init; }
    public required string TicketKey { get; init; }
    public required string FieldName { get; init; }
    public required string? FieldValue { get; init; }
    public required DateTime ObservedAt { get; init; }
    public required Guid SnapshotId { get; init; }
}
