namespace UPMS.Reporting.Delivery;

public interface IEmailReportDeliverySender
{
    Task SendAsync(EmailDeliveryMessage message, CancellationToken ct = default);
}
