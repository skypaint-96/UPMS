namespace UPMS.Data.Delivery;

public class ReportDelivery
{
    public Guid Id { get; set; }
    public Guid ReportJobId { get; set; }
    public Guid? LastBackgroundJobId { get; set; }
    public Guid CompanyProfileId { get; set; }
    public string CompanyKey { get; set; } = string.Empty;
    public string CompanyDisplayName { get; set; } = string.Empty;
    public Guid DistributionListId { get; set; }
    public string DistributionListName { get; set; } = string.Empty;
    public string Channel { get; set; } = ReportDeliveryChannels.Email;
    public string Status { get; set; } = ReportDeliveryStatuses.Queued;
    public string ArtifactPath { get; set; } = string.Empty;
    public string? ArtifactFileName { get; set; }
    public string? ArtifactContentType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string RecipientSnapshotJson { get; set; } = "[]";
    public int RecipientCount { get; set; }
    public int AttemptCount { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? LastAttemptedAt { get; set; }
    public string? LastErrorMessage { get; set; }
}
