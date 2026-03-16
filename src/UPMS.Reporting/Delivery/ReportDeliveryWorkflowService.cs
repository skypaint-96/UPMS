using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UPMS.Data;
using UPMS.Data.Artifacts;
using UPMS.Data.Delivery;
using UPMS.Data.Jobs;

namespace UPMS.Reporting.Delivery;

public sealed class ReportDeliveryWorkflowService : IReportDeliveryWorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly UpmsDbContext _context;
    private readonly IBackgroundJobService _jobs;
    private readonly IArtifactStorage _artifacts;
    private readonly IEmailReportDeliverySender _sender;
    private readonly ILogger<ReportDeliveryWorkflowService> _logger;

    public ReportDeliveryWorkflowService(
        UpmsDbContext context,
        IBackgroundJobService jobs,
        IArtifactStorage artifacts,
        IEmailReportDeliverySender sender,
        ILogger<ReportDeliveryWorkflowService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ReportDelivery>> QueueAsync(
        BackgroundJob reportJob,
        IReadOnlyCollection<Guid> distributionListIds,
        string companyName,
        string artifactPath,
        string? artifactFileName,
        string? artifactContentType,
        string? requestedBy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(reportJob);
        ArgumentException.ThrowIfNullOrWhiteSpace(companyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
        ArgumentNullException.ThrowIfNull(distributionListIds);

        var distinctIds = distributionListIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctIds.Count == 0)
            return Array.Empty<ReportDelivery>();

        var companyKey = CompanyKeyNormalizer.Normalize(companyName);
        if (string.IsNullOrWhiteSpace(companyKey))
            throw new InvalidOperationException("Report company could not be normalized into a company key.");

        var lists = await _context.DistributionLists
            .AsNoTracking()
            .Include(list => list.CompanyProfile)
            .Include(list => list.Recipients)
            .Where(list => distinctIds.Contains(list.Id))
            .ToListAsync(ct);

        var listById = lists.ToDictionary(list => list.Id);
        var utcNow = DateTime.UtcNow;
        var deliveries = new List<ReportDelivery>(distinctIds.Count);

        foreach (var listId in distinctIds)
        {
            if (!listById.TryGetValue(listId, out var list) || list.CompanyProfile is null)
            {
                _logger.LogWarning("Skipping queued delivery for unknown distribution list {DistributionListId} on report job {JobId}.", listId, reportJob.Id);
                continue;
            }

            var recipients = list.Recipients
                .Where(recipient => recipient.IsActive)
                .OrderBy(recipient => recipient.SortOrder)
                .ThenBy(recipient => recipient.Endpoint, StringComparer.OrdinalIgnoreCase)
                .Select(recipient => new ReportDeliveryRecipientSnapshot(
                    recipient.Channel,
                    recipient.Endpoint,
                    recipient.DisplayName,
                    recipient.MetadataJson))
                .ToList();

            string? failureReason = null;

            if (!list.IsActive)
            {
                failureReason = $"Distribution list '{list.Name}' is inactive.";
            }
            else if (!string.Equals(list.CompanyProfile.CompanyKey, companyKey, StringComparison.Ordinal))
            {
                failureReason = $"Distribution list '{list.Name}' belongs to company '{list.CompanyProfile.DisplayName}', not '{companyName.Trim()}'.";
            }
            else if (recipients.Count == 0)
            {
                failureReason = $"Distribution list '{list.Name}' does not have any active recipients.";
            }
            else if (recipients.Any(recipient => !string.Equals(recipient.Channel, ReportDeliveryChannels.Email, StringComparison.OrdinalIgnoreCase)))
            {
                failureReason = "Only email recipients are supported in this release.";
            }

            var delivery = new ReportDelivery
            {
                Id = Guid.NewGuid(),
                ReportJobId = reportJob.Id,
                CompanyProfileId = list.CompanyProfileId,
                CompanyKey = list.CompanyProfile.CompanyKey,
                CompanyDisplayName = list.CompanyProfile.DisplayName,
                DistributionListId = list.Id,
                DistributionListName = list.Name,
                Channel = ReportDeliveryChannels.Email,
                Status = failureReason is null ? ReportDeliveryStatuses.Queued : ReportDeliveryStatuses.Failed,
                ArtifactPath = artifactPath,
                ArtifactFileName = artifactFileName,
                ArtifactContentType = artifactContentType,
                Subject = BuildSubject(artifactFileName, list.CompanyProfile.DisplayName),
                RecipientSnapshotJson = JsonSerializer.Serialize(recipients, JsonOptions),
                RecipientCount = recipients.Count,
                RequestedBy = NormalizeOptional(requestedBy),
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
                CompletedAt = failureReason is null ? null : utcNow,
                LastErrorMessage = failureReason
            };

            _context.ReportDeliveries.Add(delivery);
            await _context.SaveChangesAsync(ct);

            if (failureReason is null)
            {
                var payload = new ReportDeliveryJobPayload(delivery.Id);
                var backgroundJob = await _jobs.EnqueueAsync(
                    BackgroundJobTypes.ReportDelivery,
                    JsonSerializer.Serialize(payload, JsonOptions),
                    requestedBy,
                    ct);

                delivery.LastBackgroundJobId = backgroundJob.Id;
                delivery.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(ct);
            }

            deliveries.Add(delivery);
        }

        return deliveries;
    }

    public async Task<IReadOnlyList<ReportDelivery>> GetRecentAsync(
        string? company = null,
        Guid? reportJobId = null,
        int take = 100,
        CancellationToken ct = default)
    {
        if (take <= 0)
            take = 100;

        var query = _context.ReportDeliveries
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(company))
        {
            var companyKey = CompanyKeyNormalizer.Normalize(company);
            query = query.Where(delivery => delivery.CompanyKey == companyKey);
        }

        if (reportJobId is Guid validReportJobId && validReportJobId != Guid.Empty)
            query = query.Where(delivery => delivery.ReportJobId == validReportJobId);

        return await query
            .OrderByDescending(delivery => delivery.CreatedAt)
            .Take(Math.Min(take, 250))
            .ToListAsync(ct);
    }

    public async Task<ReportDelivery?> GetByIdAsync(Guid deliveryId, CancellationToken ct = default)
    {
        if (deliveryId == Guid.Empty)
            return null;

        return await _context.ReportDeliveries
            .AsNoTracking()
            .FirstOrDefaultAsync(delivery => delivery.Id == deliveryId, ct);
    }

    public async Task<ReportDelivery> RetryAsync(Guid deliveryId, string? requestedBy, CancellationToken ct = default)
    {
        var delivery = await _context.ReportDeliveries
            .FirstOrDefaultAsync(row => row.Id == deliveryId, ct)
            ?? throw new KeyNotFoundException($"Report delivery '{deliveryId}' was not found.");

        if (string.Equals(delivery.Status, ReportDeliveryStatuses.Queued, StringComparison.OrdinalIgnoreCase)
            || string.Equals(delivery.Status, ReportDeliveryStatuses.Running, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected delivery is already queued or running.");
        }

        if (string.IsNullOrWhiteSpace(delivery.ArtifactPath))
            throw new InvalidOperationException("The selected delivery does not reference a report artifact.");

        if (delivery.AttemptCount == 0 && delivery.LastBackgroundJobId is null)
        {
            throw new InvalidOperationException(
                "The selected delivery failed validation before any delivery job was queued and cannot be retried. Update the distribution list selection and rerun the report.");
        }

        var payload = new ReportDeliveryJobPayload(delivery.Id);
        var backgroundJob = await _jobs.EnqueueAsync(
            BackgroundJobTypes.ReportDelivery,
            JsonSerializer.Serialize(payload, JsonOptions),
            requestedBy,
            ct);

        delivery.Status = ReportDeliveryStatuses.Queued;
        delivery.UpdatedAt = DateTime.UtcNow;
        delivery.CompletedAt = null;
        delivery.StartedAt = null;
        delivery.LastErrorMessage = null;
        delivery.LastBackgroundJobId = backgroundJob.Id;
        delivery.RequestedBy = NormalizeOptional(requestedBy) ?? delivery.RequestedBy;

        await _context.SaveChangesAsync(ct);
        return delivery;
    }

    public async Task<ReportDeliveryProcessResult> ProcessAsync(Guid deliveryId, Guid backgroundJobId, CancellationToken ct = default)
    {
        var delivery = await _context.ReportDeliveries
            .FirstOrDefaultAsync(row => row.Id == deliveryId, ct)
            ?? throw new KeyNotFoundException($"Report delivery '{deliveryId}' was not found.");

        var utcNow = DateTime.UtcNow;
        delivery.Status = ReportDeliveryStatuses.Running;
        delivery.StartedAt ??= utcNow;
        delivery.LastAttemptedAt = utcNow;
        delivery.AttemptCount += 1;
        delivery.UpdatedAt = utcNow;
        delivery.LastBackgroundJobId = backgroundJobId;
        delivery.LastErrorMessage = null;
        await _context.SaveChangesAsync(ct);

        if (!_artifacts.Exists(delivery.ArtifactPath))
        {
            return await MarkFailedAsync(delivery, $"Report artifact '{delivery.ArtifactPath}' was not found.", ct);
        }

        var recipients = DeserializeRecipients(delivery.RecipientSnapshotJson);
        if (recipients.Count == 0)
            return await MarkFailedAsync(delivery, "No recipients were available for this delivery.", ct);

        if (recipients.Any(recipient => !string.Equals(recipient.Channel, ReportDeliveryChannels.Email, StringComparison.OrdinalIgnoreCase)))
            return await MarkFailedAsync(delivery, "Only email recipients are supported in this release.", ct);

        try
        {
            var emailMessage = await BuildMessageAsync(delivery, recipients, ct);
            await _sender.SendAsync(emailMessage, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report delivery {DeliveryId} failed.", delivery.Id);
            return await MarkFailedAsync(delivery, ex.Message, ct);
        }

        delivery.Status = ReportDeliveryStatuses.Succeeded;
        delivery.CompletedAt = DateTime.UtcNow;
        delivery.UpdatedAt = delivery.CompletedAt.Value;
        delivery.LastErrorMessage = null;
        await _context.SaveChangesAsync(ct);

        return new ReportDeliveryProcessResult(true, null, recipients.Count);
    }

    private async Task<EmailDeliveryMessage> BuildMessageAsync(
        ReportDelivery delivery,
        IReadOnlyList<ReportDeliveryRecipientSnapshot> recipients,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        byte[] content;
        await using (var stream = _artifacts.OpenRead(delivery.ArtifactPath))
        await using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, ct);
            content = buffer.ToArray();
        }

        var fileName = string.IsNullOrWhiteSpace(delivery.ArtifactFileName)
            ? Path.GetFileName(delivery.ArtifactPath)
            : delivery.ArtifactFileName!;

        var contentType = delivery.ArtifactContentType;
        string? htmlBody = null;
        string? textBody = null;
        IReadOnlyList<EmailDeliveryAttachment> attachments = Array.Empty<EmailDeliveryAttachment>();

        if (IsHtml(contentType, fileName))
        {
            htmlBody = Encoding.UTF8.GetString(content);
        }
        else if (IsPlainText(contentType, fileName))
        {
            textBody = Encoding.UTF8.GetString(content);
        }
        else
        {
            attachments = [new EmailDeliveryAttachment(fileName, content, contentType)];
            textBody = BuildDefaultTextBody(delivery, fileName);
        }

        var emailRecipients = recipients
            .Select(recipient => new EmailDeliveryRecipient(recipient.Endpoint, recipient.DisplayName))
            .ToList();

        return new EmailDeliveryMessage(
            emailRecipients,
            delivery.Subject,
            htmlBody,
            textBody,
            attachments);
    }

    private async Task<ReportDeliveryProcessResult> MarkFailedAsync(
        ReportDelivery delivery,
        string errorMessage,
        CancellationToken ct)
    {
        delivery.Status = ReportDeliveryStatuses.Failed;
        delivery.CompletedAt = DateTime.UtcNow;
        delivery.UpdatedAt = delivery.CompletedAt.Value;
        delivery.LastErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "Unknown report delivery failure."
            : errorMessage.Trim();

        await _context.SaveChangesAsync(ct);
        return new ReportDeliveryProcessResult(false, delivery.LastErrorMessage, delivery.RecipientCount);
    }

    private static IReadOnlyList<ReportDeliveryRecipientSnapshot> DeserializeRecipients(string? recipientSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(recipientSnapshotJson))
            return Array.Empty<ReportDeliveryRecipientSnapshot>();

        return JsonSerializer.Deserialize<List<ReportDeliveryRecipientSnapshot>>(recipientSnapshotJson, JsonOptions)
            ?? Array.Empty<ReportDeliveryRecipientSnapshot>().ToList();
    }

    private static string BuildSubject(string? artifactFileName, string companyDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(artifactFileName))
            return $"UPMS report - {companyDisplayName} - {Path.GetFileNameWithoutExtension(artifactFileName)}";

        return $"UPMS report - {companyDisplayName}";
    }

    private static string BuildDefaultTextBody(ReportDelivery delivery, string fileName)
    {
        return string.Join(Environment.NewLine,
        [
            $"UPMS has generated the requested report for {delivery.CompanyDisplayName}.",
            $"Distribution list: {delivery.DistributionListName}.",
            $"Attached file: {fileName}.",
            string.Empty,
            "This message was sent by the UPMS report delivery worker."
        ]);
    }

    private static bool IsHtml(string? contentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            return true;

        var extension = Path.GetExtension(fileName);
        return string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPlainText(string? contentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType) && contentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(Path.GetExtension(fileName), ".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ReportDeliveryRecipientSnapshot(
        string Channel,
        string Endpoint,
        string? DisplayName,
        string? MetadataJson);
}
