namespace UPMS.Data;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UPMS.Data.Jobs;

public sealed class FileSharePollingSourceService : IFileSharePollingSourceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly UpmsDbContext _context;
    private readonly FileSharePollingOptions _options;

    public FileSharePollingSourceService(
        UpmsDbContext context,
        IOptions<FileSharePollingOptions> options)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options?.Value ?? new FileSharePollingOptions();
    }

    public async Task<IReadOnlyList<FileSharePollingSource>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.FileSharePollingSources
            .AsNoTracking()
            .OrderBy(source => source.Name)
            .ToListAsync(ct);
    }

    public async Task<FileSharePollingSource?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty)
            return null;

        return await _context.FileSharePollingSources
            .AsNoTracking()
            .FirstOrDefaultAsync(source => source.Id == id, ct);
    }

    public async Task<IReadOnlyList<FileSharePollingSource>> GetDueAsync(DateTime utcNow, int take = 100, CancellationToken ct = default)
    {
        int boundedTake = take <= 0 ? 100 : Math.Min(take, 500);

        return await _context.FileSharePollingSources
            .AsNoTracking()
            .Where(source => source.Enabled && (source.NextPollDueAt == null || source.NextPollDueAt <= utcNow))
            .OrderBy(source => source.NextPollDueAt ?? DateTime.MinValue)
            .ThenBy(source => source.Name)
            .Take(boundedTake)
            .ToListAsync(ct);
    }

    public async Task<FileSharePollingSource> CreateAsync(FileSharePollingSourceUpsert request, string? requestedBy, CancellationToken ct = default)
    {
        EnsureUserManagedSourcesEnabled();

        var source = new FileSharePollingSource
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = ResolveRequestedBy(requestedBy)
        };

        await ApplyUpsertAsync(source, request, isCreate: true, ct);

        _context.FileSharePollingSources.Add(source);
        await _context.SaveChangesAsync(ct);
        return source;
    }

    public async Task<FileSharePollingSource> UpdateAsync(Guid id, FileSharePollingSourceUpsert request, string? requestedBy, CancellationToken ct = default)
    {
        EnsureUserManagedSourcesEnabled();

        var source = await _context.FileSharePollingSources
            .FirstOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new KeyNotFoundException($"File share polling source '{id}' was not found.");

        await EnsureNoActiveJobAsync(source, ct);
        await ApplyUpsertAsync(source, request, isCreate: false, ct);

        source.UpdatedAt = DateTime.UtcNow;
        source.UpdatedBy = ResolveRequestedBy(requestedBy);
        await _context.SaveChangesAsync(ct);
        return source;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        EnsureUserManagedSourcesEnabled();

        var source = await _context.FileSharePollingSources
            .FirstOrDefaultAsync(row => row.Id == id, ct)
            ?? throw new KeyNotFoundException($"File share polling source '{id}' was not found.");

        await EnsureNoActiveJobAsync(source, ct);

        _context.FileSharePollingSources.Remove(source);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<FileSharePollingEnqueueResult> QueuePollJobAsync(Guid sourceId, bool triggeredManually, string? requestedBy, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("File share polling is disabled by configuration.");

        var source = await _context.FileSharePollingSources
            .FirstOrDefaultAsync(row => row.Id == sourceId, ct)
            ?? throw new KeyNotFoundException($"File share polling source '{sourceId}' was not found.");

        if (!triggeredManually && !source.Enabled)
            throw new InvalidOperationException($"File share polling source '{source.Name}' is disabled.");

        var activeJob = await TryGetActiveJobAsync(source, ct);
        if (activeJob is not null)
            return new FileSharePollingEnqueueResult(activeJob, true);

        var payload = new FileSharePollJobPayload(source.Id, triggeredManually);
        var job = new BackgroundJob
        {
            Id = Guid.NewGuid(),
            JobType = BackgroundJobTypes.FileSharePoll,
            Status = BackgroundJobStatuses.Pending,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            RequestedBy = string.IsNullOrWhiteSpace(requestedBy) ? null : requestedBy.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        source.CurrentJobId = job.Id;
        source.LastJobId = job.Id;
        source.LastError = null;
        source.NextPollDueAt = source.Enabled
            ? DateTime.UtcNow.AddSeconds(Math.Max(source.PollIntervalSeconds, 1))
            : null;

        _context.BackgroundJobs.Add(job);
        await _context.SaveChangesAsync(ct);
        return new FileSharePollingEnqueueResult(job, false);
    }

    public async Task MarkRunStartedAsync(Guid sourceId, Guid jobId, CancellationToken ct = default)
    {
        var source = await _context.FileSharePollingSources.FirstOrDefaultAsync(row => row.Id == sourceId, ct);
        if (source is null)
            return;

        source.CurrentJobId = jobId;
        source.LastJobId = jobId;
        source.LastRunStartedAt = DateTime.UtcNow;
        source.LastError = null;
        await _context.SaveChangesAsync(ct);
    }

    public async Task MarkRunCompletedAsync(Guid sourceId, Guid jobId, bool succeeded, string? errorMessage, CancellationToken ct = default)
    {
        var source = await _context.FileSharePollingSources.FirstOrDefaultAsync(row => row.Id == sourceId, ct);
        if (source is null)
            return;

        if (source.CurrentJobId == jobId)
            source.CurrentJobId = null;

        source.LastJobId = jobId;
        source.LastRunCompletedAt = DateTime.UtcNow;
        source.NextPollDueAt = source.Enabled
            ? DateTime.UtcNow.AddSeconds(Math.Max(source.PollIntervalSeconds, 1))
            : null;

        if (succeeded)
        {
            source.LastSucceededAt = source.LastRunCompletedAt;
            source.LastError = null;
        }
        else
        {
            source.LastError = string.IsNullOrWhiteSpace(errorMessage)
                ? "File share polling failed."
                : errorMessage.Trim();
        }

        await _context.SaveChangesAsync(ct);
    }

    private void EnsureUserManagedSourcesEnabled()
    {
        if (!_options.AllowUserManagedSources)
            throw new InvalidOperationException("User-managed file share polling sources are disabled by configuration.");
    }

    private async Task ApplyUpsertAsync(FileSharePollingSource source, FileSharePollingSourceUpsert request, bool isCreate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        string name = string.IsNullOrWhiteSpace(request.Name)
            ? throw new InvalidOperationException("Name is required.")
            : request.Name.Trim();

        string watchedPath = NormalizeRequiredPath(request.WatchedPath, "WatchedPath");
        string archivePath = NormalizeRequiredPath(request.ArchivePath, "ArchivePath");
        string errorPath = NormalizeRequiredPath(request.ErrorPath, "ErrorPath");
        string itsmSource = string.IsNullOrWhiteSpace(request.ItsmSource)
            ? throw new InvalidOperationException("ItsmSource is required.")
            : request.ItsmSource.Trim();

        int pollIntervalSeconds = request.PollIntervalSeconds ?? _options.DefaultPollIntervalSeconds;
        int stableFileAgeSeconds = request.StableFileAgeSeconds ?? _options.DefaultStableFileAgeSeconds;
        int? maxFilesPerCycle = NormalizeMaxFilesPerCycle(request.MaxFilesPerCycle);
        var patterns = NormalizePatterns(request.FilePatterns);

        ValidatePathPolicy("watched path", watchedPath, _options.AllowedWatchedRoots);
        ValidatePathPolicy("archive path", archivePath, _options.AllowedArchiveRoots);
        ValidatePathPolicy("error path", errorPath, _options.AllowedErrorRoots);

        if (string.Equals(watchedPath, archivePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WatchedPath and ArchivePath must be different.");

        if (string.Equals(watchedPath, errorPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("WatchedPath and ErrorPath must be different.");

        if (string.Equals(archivePath, errorPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ArchivePath and ErrorPath must be different.");

        if (pollIntervalSeconds < Math.Max(_options.MinPollIntervalSeconds, 1)
            || pollIntervalSeconds > Math.Max(_options.MaxPollIntervalSeconds, _options.MinPollIntervalSeconds))
        {
            throw new InvalidOperationException(
                $"PollIntervalSeconds must be between {_options.MinPollIntervalSeconds} and {_options.MaxPollIntervalSeconds}.");
        }

        if (stableFileAgeSeconds < 0)
            throw new InvalidOperationException("StableFileAgeSeconds must be zero or greater.");

        bool itsmSourceExists = await _context.ItsmSources
            .AsNoTracking()
            .AnyAsync(row => row.Name == itsmSource, ct);

        if (!itsmSourceExists)
            throw new InvalidOperationException($"ITSM source '{itsmSource}' was not found.");

        bool duplicateNameExists = await _context.FileSharePollingSources
            .AsNoTracking()
            .AnyAsync(row => row.Id != source.Id && row.Name.ToLower() == name.ToLower(), ct);

        if (duplicateNameExists)
            throw new InvalidOperationException($"A file share polling source named '{name}' already exists.");

        bool duplicateWatchPathExists = await _context.FileSharePollingSources
            .AsNoTracking()
            .AnyAsync(row => row.Id != source.Id && row.WatchedPath.ToLower() == watchedPath.ToLower(), ct);

        if (duplicateWatchPathExists)
        {
            throw new InvalidOperationException(
                $"A file share polling source for watched path '{watchedPath}' already exists. Reuse the existing source instead of overlapping scans.");
        }

        source.Name = name;
        source.Enabled = request.Enabled;
        source.WatchedPath = watchedPath;
        source.ArchivePath = archivePath;
        source.ErrorPath = errorPath;
        source.ItsmSource = itsmSource;
        source.PollIntervalSeconds = pollIntervalSeconds;
        source.MaxFilesPerCycle = maxFilesPerCycle;
        source.StableFileAgeSeconds = stableFileAgeSeconds;
        source.SetFilePatterns(patterns);

        if (isCreate)
        {
            source.NextPollDueAt = source.Enabled ? DateTime.UtcNow : null;
        }
        else if (!source.Enabled)
        {
            source.NextPollDueAt = null;
        }
        else if (source.CurrentJobId is null)
        {
            source.NextPollDueAt = DateTime.UtcNow;
        }
    }

    private async Task EnsureNoActiveJobAsync(FileSharePollingSource source, CancellationToken ct)
    {
        var activeJob = await TryGetActiveJobAsync(source, ct);
        if (activeJob is not null)
        {
            throw new InvalidOperationException(
                $"File share polling source '{source.Name}' currently has an active poll job ({activeJob.Id}). Wait for it to complete before editing or deleting the source.");
        }
    }

    private async Task<BackgroundJob?> TryGetActiveJobAsync(FileSharePollingSource source, CancellationToken ct)
    {
        if (!source.CurrentJobId.HasValue)
            return null;

        var job = await _context.BackgroundJobs.FirstOrDefaultAsync(row => row.Id == source.CurrentJobId.Value, ct);
        if (job is not null
            && (job.Status == BackgroundJobStatuses.Pending || job.Status == BackgroundJobStatuses.Running))
        {
            return job;
        }

        source.CurrentJobId = null;
        await _context.SaveChangesAsync(ct);
        return null;
    }

    private int? NormalizeMaxFilesPerCycle(int? maxFilesPerCycle)
    {
        if (!maxFilesPerCycle.HasValue)
            return null;

        if (maxFilesPerCycle.Value <= 0)
            throw new InvalidOperationException("MaxFilesPerCycle must be greater than zero when provided.");

        if (maxFilesPerCycle.Value > Math.Max(_options.MaxFilesPerCycleCap, 1))
        {
            throw new InvalidOperationException(
                $"MaxFilesPerCycle must be less than or equal to {_options.MaxFilesPerCycleCap}.");
        }

        return maxFilesPerCycle.Value;
    }

    private static string ResolveRequestedBy(string? requestedBy)
        => string.IsNullOrWhiteSpace(requestedBy) ? "anonymous" : requestedBy.Trim();

    private static string NormalizeRequiredPath(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{fieldName} is required.");

        try
        {
            return Path.GetFullPath(value.Trim());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"{fieldName} is not a valid path. {ex.Message}");
        }
    }

    private static IReadOnlyList<string> NormalizePatterns(IReadOnlyList<string>? patterns)
    {
        var normalized = FileSharePollingSource.NormalizePatterns(patterns);
        foreach (var pattern in normalized)
        {
            if (pattern.IndexOf(Path.DirectorySeparatorChar) >= 0 || pattern.IndexOf(Path.AltDirectorySeparatorChar) >= 0)
            {
                throw new InvalidOperationException("File patterns must only match file names and may not contain directory separators.");
            }
        }

        return normalized;
    }

    private static void ValidatePathPolicy(string label, string candidatePath, IReadOnlyList<string>? allowedRoots)
    {
        if (allowedRoots is null || allowedRoots.Count == 0)
            return;

        var normalizedRoots = allowedRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.GetFullPath(root.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedRoots.Length == 0)
            return;

        if (normalizedRoots.Any(root => IsPathUnderRoot(candidatePath, root)))
            return;

        throw new InvalidOperationException(
            $"The configured {label} '{candidatePath}' is outside the allowed root(s): {string.Join(", ", normalizedRoots)}.");
    }

    private static bool IsPathUnderRoot(string candidatePath, string rootPath)
    {
        if (string.Equals(candidatePath, rootPath, StringComparison.OrdinalIgnoreCase))
            return true;

        string rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar) || rootPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        return candidatePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
