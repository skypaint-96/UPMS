namespace UPMS.Web.Reporting;

using UPMS.Data;

public sealed class TicketLifecycleInfo
{
    public required Ticket Ticket { get; init; }
    public DateTime? OpenedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public required string Status { get; init; }
    public required string Priority { get; init; }
    public double? AgeDays { get; init; }
}
