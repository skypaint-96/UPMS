namespace UPMS.Data.Jobs;

using System.Text.Json;
using UPMS.Data.Artifacts;

public sealed class SnapshotIngestJobSubmissionService : ISnapshotIngestJobSubmissionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IArtifactStorage _artifacts;
    private readonly IBackgroundJobService _jobs;

    public SnapshotIngestJobSubmissionService(
        IArtifactStorage artifacts,
        IBackgroundJobService jobs)
    {
        _artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
    }

    public async Task<BackgroundJob> QueueAsync(
        Stream content,
        string fileName,
        string? contentType,
        string itsmSource,
        DateOnly snapshotDate,
        string? requestedBy,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSource);

        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? ResolveContentType(fileName)
            : contentType.Trim();

        var stored = await _artifacts.SaveAsync(
            "uploads",
            fileName.Trim(),
            content,
            normalizedContentType,
            ct);

        var payload = new SnapshotIngestJobPayload(
            itsmSource.Trim(),
            snapshotDate,
            stored.RelativePath,
            fileName.Trim(),
            normalizedContentType);

        return await _jobs.EnqueueAsync(
            BackgroundJobTypes.SnapshotIngest,
            JsonSerializer.Serialize(payload, JsonOptions),
            string.IsNullOrWhiteSpace(requestedBy) ? null : requestedBy.Trim(),
            ct);
    }

    private static string ResolveContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".json" => "application/json",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }
}
