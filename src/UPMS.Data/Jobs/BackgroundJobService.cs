namespace UPMS.Data.Jobs;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class BackgroundJobService : IBackgroundJobService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly UpmsDbContext _context;

    public BackgroundJobService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<BackgroundJob> EnqueueAsync(
        string jobType,
        string payloadJson,
        string? requestedBy,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        var job = new BackgroundJob
        {
            Id = Guid.NewGuid(),
            JobType = jobType.Trim(),
            Status = BackgroundJobStatuses.Pending,
            PayloadJson = payloadJson,
            RequestedBy = string.IsNullOrWhiteSpace(requestedBy) ? null : requestedBy.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.BackgroundJobs.Add(job);
        await _context.SaveChangesAsync(ct);
        return job;
    }

    public async Task<BackgroundJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return null;

        return await _context.BackgroundJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, ct);
    }

    public async Task<IReadOnlyList<BackgroundJob>> GetRecentAsync(int take = 50, CancellationToken ct = default)
    {
        if (take <= 0)
            take = 50;

        return await _context.BackgroundJobs
            .AsNoTracking()
            .OrderByDescending(j => j.CreatedAt)
            .Take(Math.Min(take, 250))
            .ToListAsync(ct);
    }

    public async Task<BackgroundJob?> TryClaimNextAsync(
        string workerName,
        IReadOnlyCollection<string> supportedJobTypes,
        TimeSpan leaseDuration,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerName);
        ArgumentNullException.ThrowIfNull(supportedJobTypes);

        if (supportedJobTypes.Count == 0)
            return null;

        var utcNow = DateTime.UtcNow;
        var normalizedTypes = supportedJobTypes
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedTypes.Count == 0)
            return null;

        if (_context.Database.IsRelational())
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var nextRelationalJob = await _context.BackgroundJobs
                .Where(j => normalizedTypes.Contains(j.JobType)
                    && (j.Status == BackgroundJobStatuses.Pending
                        || (j.Status == BackgroundJobStatuses.Running
                            && j.LeaseExpiresAt != null
                            && j.LeaseExpiresAt < utcNow)))
                .OrderBy(j => j.CreatedAt)
                .ThenBy(j => j.Id)
                .FirstOrDefaultAsync(ct);

            if (nextRelationalJob is null)
            {
                await transaction.CommitAsync(ct);
                return null;
            }

            nextRelationalJob.Status = BackgroundJobStatuses.Running;
            nextRelationalJob.StartedAt ??= utcNow;
            nextRelationalJob.LeaseOwner = workerName.Trim();
            nextRelationalJob.LeaseExpiresAt = utcNow.Add(leaseDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : leaseDuration);
            nextRelationalJob.ErrorMessage = null;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return nextRelationalJob;
        }

        var nextJob = await _context.BackgroundJobs
            .Where(j => normalizedTypes.Contains(j.JobType)
                && (j.Status == BackgroundJobStatuses.Pending
                    || (j.Status == BackgroundJobStatuses.Running
                        && j.LeaseExpiresAt != null
                        && j.LeaseExpiresAt < utcNow)))
            .OrderBy(j => j.CreatedAt)
            .ThenBy(j => j.Id)
            .FirstOrDefaultAsync(ct);

        if (nextJob is null)
            return null;

        nextJob.Status = BackgroundJobStatuses.Running;
        nextJob.StartedAt ??= utcNow;
        nextJob.LeaseOwner = workerName.Trim();
        nextJob.LeaseExpiresAt = utcNow.Add(leaseDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : leaseDuration);
        nextJob.ErrorMessage = null;

        await _context.SaveChangesAsync(ct);
        return nextJob;
    }

    public async Task<BackgroundJob?> FindMatchingSnapshotIngestAsync(
        string itsmSource,
        DateOnly snapshotDate,
        SnapshotIngestMetadata metadata,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSource);
        ArgumentNullException.ThrowIfNull(metadata);

        if (!SnapshotIngestMetadataHelper.HasDuplicateKeys(metadata))
            return null;

        var normalizedSource = itsmSource.Trim();
        var candidates = await _context.BackgroundJobs
            .AsNoTracking()
            .Where(job => job.JobType == BackgroundJobTypes.SnapshotIngest
                && job.Status != BackgroundJobStatuses.Failed)
            .OrderByDescending(job => job.CreatedAt)
            .Take(250)
            .ToListAsync(ct);

        foreach (var candidate in candidates)
        {
            SnapshotIngestJobPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<SnapshotIngestJobPayload>(candidate.PayloadJson, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (payload is null)
                continue;

            if (!string.Equals(payload.ItsmSource, normalizedSource, StringComparison.Ordinal))
                continue;

            if (payload.SnapshotDate != snapshotDate)
                continue;

            var candidateMetadata = SnapshotIngestMetadataHelper.Normalize(
                payload.Metadata,
                payload.OriginalFileName,
                payload.ContentType,
                SnapshotUploadChannels.ManualJob,
                automated: false,
                payload.ArtifactPath,
                payload.Metadata?.ContentSha256,
                candidate.RequestedBy);

            if (SnapshotIngestMetadataHelper.TryGetDuplicateReason(candidateMetadata, metadata, out _))
                return candidate;
        }

        return null;
    }

    public async Task MarkSucceededAsync(
        Guid id,
        string? resultJson,
        string? outputFilePath,
        string? outputFileName,
        string? outputContentType,
        CancellationToken ct = default)
    {
        var job = await _context.BackgroundJobs.FirstOrDefaultAsync(j => j.Id == id, ct)
            ?? throw new KeyNotFoundException($"Background job '{id}' was not found.");

        job.Status = BackgroundJobStatuses.Succeeded;
        job.CompletedAt = DateTime.UtcNow;
        job.ResultJson = resultJson;
        job.OutputFilePath = outputFilePath;
        job.OutputFileName = outputFileName;
        job.OutputContentType = outputContentType;
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;
        job.ErrorMessage = null;

        await _context.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid id, string errorMessage, CancellationToken ct = default)
    {
        var job = await _context.BackgroundJobs.FirstOrDefaultAsync(j => j.Id == id, ct)
            ?? throw new KeyNotFoundException($"Background job '{id}' was not found.");

        job.Status = BackgroundJobStatuses.Failed;
        job.CompletedAt = DateTime.UtcNow;
        job.ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "Unknown background job failure." : errorMessage.Trim();
        job.LeaseOwner = null;
        job.LeaseExpiresAt = null;

        await _context.SaveChangesAsync(ct);
    }

    public async Task RenewLeaseAsync(Guid id, string workerName, TimeSpan leaseDuration, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workerName);

        var job = await _context.BackgroundJobs.FirstOrDefaultAsync(j => j.Id == id, ct)
            ?? throw new KeyNotFoundException($"Background job '{id}' was not found.");

        job.LeaseOwner = workerName.Trim();
        job.LeaseExpiresAt = DateTime.UtcNow.Add(leaseDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(5) : leaseDuration);

        await _context.SaveChangesAsync(ct);
    }
}
