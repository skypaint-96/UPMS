namespace UPMS.Data.ReadModels;

/// <summary>
/// Keyless DTO for rows returned by <c>batch_reconstruct_tickets</c>
/// and <c>batch_reconstruct_for_snapshot</c>.
/// </summary>
public class ReconstructedFieldDto
{
    public string TicketKey { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
}
