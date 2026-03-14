namespace UPMS.Data.Delivery;

public class DistributionList
{
    public Guid Id { get; set; }
    public Guid CompanyProfileId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public CompanyProfile? CompanyProfile { get; set; }
    public ICollection<DistributionListRecipient> Recipients { get; set; } = new List<DistributionListRecipient>();
}
