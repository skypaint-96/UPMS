namespace UPMS.Data.Delivery;

public class CompanyProfile
{
    public Guid Id { get; set; }
    public string CompanyKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<DistributionList> DistributionLists { get; set; } = new List<DistributionList>();
}
