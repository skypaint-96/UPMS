namespace UPMS.Data.Delivery;

public class DistributionListRecipient
{
    public Guid Id { get; set; }
    public Guid DistributionListId { get; set; }
    public string Channel { get; set; } = ReportDeliveryChannels.Email;
    public string Endpoint { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? MetadataJson { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DistributionList? DistributionList { get; set; }
}
