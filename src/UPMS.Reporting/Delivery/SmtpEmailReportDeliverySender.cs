using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace UPMS.Reporting.Delivery;

public sealed class SmtpEmailReportDeliverySender : IEmailReportDeliverySender
{
    private readonly EmailDeliveryOptions _options;

    public SmtpEmailReportDeliverySender(IOptions<EmailDeliveryOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? new EmailDeliveryOptions();
    }

    public async Task SendAsync(EmailDeliveryMessage message, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        ct.ThrowIfCancellationRequested();

        var mode = (_options.Mode ?? string.Empty).Trim();
        if (string.Equals(mode, "Disabled", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(mode))
        {
            throw new InvalidOperationException("Email delivery is disabled. Configure Delivery:Email:Mode as 'Smtp' or 'PickupDirectory'.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromAddress))
            throw new InvalidOperationException("Delivery:Email:FromAddress must be configured.");

        if (message.To.Count == 0)
            throw new InvalidOperationException("At least one recipient is required.");

        using var mail = new MailMessage
        {
            From = string.IsNullOrWhiteSpace(_options.FromDisplayName)
                ? new MailAddress(_options.FromAddress.Trim())
                : new MailAddress(_options.FromAddress.Trim(), _options.FromDisplayName.Trim()),
            Subject = ApplySubjectPrefix(message.Subject),
            Body = message.HtmlBody ?? message.TextBody ?? string.Empty,
            IsBodyHtml = message.HtmlBody is not null
        };

        foreach (var recipient in message.To
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient.Address))
            .DistinctBy(recipient => recipient.Address, StringComparer.OrdinalIgnoreCase))
        {
            mail.To.Add(string.IsNullOrWhiteSpace(recipient.DisplayName)
                ? new MailAddress(recipient.Address.Trim())
                : new MailAddress(recipient.Address.Trim(), recipient.DisplayName.Trim()));
        }

        foreach (var attachment in message.Attachments)
        {
            var stream = new MemoryStream(attachment.Content, writable: false);
            mail.Attachments.Add(new Attachment(
                stream,
                string.IsNullOrWhiteSpace(attachment.FileName) ? "upms-report.bin" : attachment.FileName,
                attachment.ContentType ?? "application/octet-stream"));
        }

        using var client = BuildClient(mode);
        ct.ThrowIfCancellationRequested();
        await client.SendMailAsync(mail);
    }

    private SmtpClient BuildClient(string mode)
    {
        if (string.Equals(mode, "PickupDirectory", StringComparison.OrdinalIgnoreCase))
        {
            var pickupDirectory = string.IsNullOrWhiteSpace(_options.PickupDirectory)
                ? Path.Combine(Path.GetTempPath(), "upms-email-pickup")
                : _options.PickupDirectory.Trim();

            Directory.CreateDirectory(pickupDirectory);

            return new SmtpClient
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = pickupDirectory,
                Timeout = Math.Max(_options.TimeoutMilliseconds, 1000)
            };
        }

        if (!string.Equals(mode, "Smtp", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unsupported Delivery:Email:Mode '{mode}'. Expected 'Smtp' or 'PickupDirectory'.");

        if (string.IsNullOrWhiteSpace(_options.Host))
            throw new InvalidOperationException("Delivery:Email:Host must be configured when SMTP mode is enabled.");

        var client = new SmtpClient(_options.Host.Trim(), _options.Port <= 0 ? 25 : _options.Port)
        {
            DeliveryMethod = SmtpDeliveryMethod.Network,
            EnableSsl = _options.UseSsl,
            Timeout = Math.Max(_options.TimeoutMilliseconds, 1000)
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_options.Username.Trim(), _options.Password ?? string.Empty);
        }

        return client;
    }

    private string ApplySubjectPrefix(string subject)
    {
        var normalizedSubject = string.IsNullOrWhiteSpace(subject) ? "UPMS report" : subject.Trim();
        if (string.IsNullOrWhiteSpace(_options.SubjectPrefix))
            return normalizedSubject;

        return $"{_options.SubjectPrefix.Trim()} {normalizedSubject}".Trim();
    }
}
