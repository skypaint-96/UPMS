namespace UPMS.Reporting.Delivery;

public sealed class EmailDeliveryOptions
{
    public string Mode { get; set; } = "Disabled";
    public string? Host { get; set; }
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? PickupDirectory { get; set; }
    public string? FromAddress { get; set; }
    public string? FromDisplayName { get; set; } = "UPMS";
    public string? SubjectPrefix { get; set; } = "[UPMS]";
    public int TimeoutMilliseconds { get; set; } = 10000;
}

public sealed record EmailDeliveryRecipient(string Address, string? DisplayName);

public sealed record EmailDeliveryAttachment(string FileName, byte[] Content, string? ContentType);

public sealed record EmailDeliveryMessage(
    IReadOnlyList<EmailDeliveryRecipient> To,
    string Subject,
    string? HtmlBody,
    string? TextBody,
    IReadOnlyList<EmailDeliveryAttachment> Attachments);
