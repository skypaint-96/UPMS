namespace UPMS.Data.ReadModels;

/// <summary>
/// Keyless DTO for rows returned by <c>get_ticket_at_time_with_metadata</c>.
/// </summary>
public class TicketFieldWithMetadataDto
{
    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
    public DateTime ObservedAt { get; set; }
    public Guid SnapshotId { get; set; }
}
