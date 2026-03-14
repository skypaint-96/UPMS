namespace UPMS.Data;

using System.Text.Json;

public static class SnapshotUploadChannels
{
    public const string ManualSync = "manual-sync";
    public const string ManualJob = "manual-job";
    public const string ManualBulkJob = "manual-bulk-job";
    public const string AutomatedApi = "api-automated";
}

public sealed record SnapshotIngestMetadata
{
    public string UploadChannel { get; init; } = SnapshotUploadChannels.ManualJob;
    public string? SourceSystem { get; init; }
    public string? Producer { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string? CorrelationId { get; init; }
    public DateTime SubmittedAtUtc { get; init; }
    public DateTime? SourceTimestampUtc { get; init; }
    public string? ContentType { get; init; }
    public string? ContentSha256 { get; init; }
    public string? SourceFileIdentity { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? ArtifactPath { get; init; }
    public string? RequestedBy { get; init; }
    public bool IsAutomated { get; init; }
}

public static class SnapshotIngestMetadataHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static SnapshotIngestMetadata Normalize(
        SnapshotIngestMetadata? metadata,
        string originalFileName,
        string? contentType,
        string defaultUploadChannel,
        bool automated,
        string? artifactPath,
        string? contentSha256,
        string? requestedBy = null)
    {
        var normalizedUploadChannel = NormalizeOptional(metadata?.UploadChannel) ?? defaultUploadChannel;
        var normalizedOriginalFileName = NormalizeFileName(metadata?.OriginalFileName) ?? NormalizeFileName(originalFileName) ?? "snapshot.bin";
        var normalizedSubmittedAtUtc = NormalizeUtc(metadata?.SubmittedAtUtc) ?? DateTime.UtcNow;
        var normalizedSourceTimestampUtc = NormalizeUtc(metadata?.SourceTimestampUtc);
        var normalizedContentType = NormalizeOptional(metadata?.ContentType) ?? NormalizeOptional(contentType);
        var normalizedArtifactPath = NormalizeOptional(metadata?.ArtifactPath) ?? NormalizeOptional(artifactPath);
        var normalizedRequestedBy = NormalizeOptional(metadata?.RequestedBy) ?? NormalizeOptional(requestedBy);

        return new SnapshotIngestMetadata
        {
            UploadChannel = normalizedUploadChannel,
            SourceSystem = NormalizeOptional(metadata?.SourceSystem),
            Producer = NormalizeOptional(metadata?.Producer),
            OriginalFileName = normalizedOriginalFileName,
            CorrelationId = NormalizeOptional(metadata?.CorrelationId),
            SubmittedAtUtc = normalizedSubmittedAtUtc,
            SourceTimestampUtc = normalizedSourceTimestampUtc,
            ContentType = normalizedContentType,
            ContentSha256 = NormalizeHash(metadata?.ContentSha256 ?? contentSha256),
            SourceFileIdentity = NormalizeOptional(metadata?.SourceFileIdentity),
            IdempotencyKey = NormalizeOptional(metadata?.IdempotencyKey),
            ArtifactPath = normalizedArtifactPath,
            RequestedBy = normalizedRequestedBy,
            IsAutomated = metadata?.IsAutomated ?? automated
        };
    }

    public static string? Serialize(SnapshotIngestMetadata? metadata)
    {
        return metadata is null ? null : JsonSerializer.Serialize(metadata, JsonOptions);
    }

    public static SnapshotIngestMetadata? Deserialize(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SnapshotIngestMetadata>(payload, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static bool HasDuplicateKeys(SnapshotIngestMetadata? metadata)
    {
        return !string.IsNullOrWhiteSpace(NormalizeOptional(metadata?.IdempotencyKey))
            || !string.IsNullOrWhiteSpace(NormalizeOptional(metadata?.SourceFileIdentity))
            || !string.IsNullOrWhiteSpace(NormalizeHash(metadata?.ContentSha256));
    }

    public static bool TryGetDuplicateReason(
        SnapshotIngestMetadata? existing,
        SnapshotIngestMetadata? incoming,
        out string? reason)
    {
        reason = null;

        if (!HasDuplicateKeys(existing) || !HasDuplicateKeys(incoming))
            return false;

        var existingIdempotencyKey = NormalizeOptional(existing?.IdempotencyKey);
        var incomingIdempotencyKey = NormalizeOptional(incoming?.IdempotencyKey);
        if (!string.IsNullOrWhiteSpace(existingIdempotencyKey)
            && !string.IsNullOrWhiteSpace(incomingIdempotencyKey)
            && string.Equals(existingIdempotencyKey, incomingIdempotencyKey, StringComparison.Ordinal))
        {
            reason = "Matching idempotency key.";
            return true;
        }

        var existingSourceFileIdentity = NormalizeOptional(existing?.SourceFileIdentity);
        var incomingSourceFileIdentity = NormalizeOptional(incoming?.SourceFileIdentity);
        var existingHash = NormalizeHash(existing?.ContentSha256);
        var incomingHash = NormalizeHash(incoming?.ContentSha256);

        if (!string.IsNullOrWhiteSpace(existingSourceFileIdentity)
            && !string.IsNullOrWhiteSpace(incomingSourceFileIdentity)
            && string.Equals(existingSourceFileIdentity, incomingSourceFileIdentity, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(existingHash)
                || string.IsNullOrWhiteSpace(incomingHash)
                || string.Equals(existingHash, incomingHash, StringComparison.OrdinalIgnoreCase))
            {
                reason = string.IsNullOrWhiteSpace(existingHash) || string.IsNullOrWhiteSpace(incomingHash)
                    ? "Matching source file identity."
                    : "Matching source file identity and content checksum.";
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(existingHash)
            && !string.IsNullOrWhiteSpace(incomingHash)
            && string.Equals(existingHash, incomingHash, StringComparison.OrdinalIgnoreCase))
        {
            reason = "Matching content checksum.";
            return true;
        }

        return false;
    }

    public static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string? NormalizeHash(string? hash)
    {
        return string.IsNullOrWhiteSpace(hash) ? null : hash.Trim().ToLowerInvariant();
    }

    private static string? NormalizeFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var trimmed = fileName.Trim();
        return Path.GetFileName(trimmed);
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (value is null || value == default)
            return null;

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }
}
