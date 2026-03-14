namespace UPMS.Ingestion;

using Microsoft.EntityFrameworkCore;
using UPMS.Data;

public sealed class SnapshotDuplicateDetector : ISnapshotDuplicateDetector
{
    private readonly UpmsDbContext _dbContext;

    public SnapshotDuplicateDetector(UpmsDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<SnapshotDuplicateMatch?> FindExistingSnapshotAsync(
        string itsmSourceName,
        DateOnly snapshotDate,
        SnapshotIngestMetadata? metadata,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSourceName);

        if (!SnapshotIngestMetadataHelper.HasDuplicateKeys(metadata))
            return null;

        var snapshotDateUtc = snapshotDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var normalizedSource = itsmSourceName.Trim();
        var candidates = await _dbContext.Snapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.ItsmSource == normalizedSource && snapshot.SnapshotDate == snapshotDateUtc)
            .OrderByDescending(snapshot => snapshot.UploadedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var candidate in candidates)
        {
            var existingMetadata = SnapshotIngestMetadataHelper.Deserialize(candidate.UploadMetadata);
            if (SnapshotIngestMetadataHelper.TryGetDuplicateReason(existingMetadata, metadata, out var reason))
            {
                return new SnapshotDuplicateMatch(candidate.Id, reason ?? "Duplicate snapshot upload detected.");
            }
        }

        return null;
    }
}
