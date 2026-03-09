namespace UPMS.Data.Jobs;

/// <summary>
/// Represents a queued background job processed by <c>UPMS.Worker</c>.
/// </summary>
public class BackgroundJob
{
    public Guid Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Status { get; set; } = BackgroundJobStatuses.Pending;
    public string PayloadJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string? RequestedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public string? OutputFilePath { get; set; }
    public string? OutputFileName { get; set; }
    public string? OutputContentType { get; set; }
}
