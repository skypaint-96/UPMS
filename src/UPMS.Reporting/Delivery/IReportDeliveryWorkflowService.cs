using UPMS.Data.Delivery;
using UPMS.Data.Jobs;

namespace UPMS.Reporting.Delivery;

public interface IReportDeliveryWorkflowService
{
    Task<IReadOnlyList<ReportDelivery>> QueueAsync(
        BackgroundJob reportJob,
        IReadOnlyCollection<Guid> distributionListIds,
        string companyName,
        string artifactPath,
        string? artifactFileName,
        string? artifactContentType,
        string? requestedBy,
        CancellationToken ct = default);

    Task<IReadOnlyList<ReportDelivery>> GetRecentAsync(
        string? company = null,
        Guid? reportJobId = null,
        int take = 100,
        CancellationToken ct = default);

    Task<ReportDelivery?> GetByIdAsync(Guid deliveryId, CancellationToken ct = default);
    Task<ReportDelivery> RetryAsync(Guid deliveryId, string? requestedBy, CancellationToken ct = default);
    Task<ReportDeliveryProcessResult> ProcessAsync(Guid deliveryId, Guid backgroundJobId, CancellationToken ct = default);
}

public sealed record ReportDeliveryProcessResult(bool Success, string? ErrorMessage, int RecipientCount);
