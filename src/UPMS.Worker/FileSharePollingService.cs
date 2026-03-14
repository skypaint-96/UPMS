namespace UPMS.Worker;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UPMS.Data.Jobs;

public sealed partial class FileSharePollingService : BackgroundService
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly TimeSpan StaleClaimAge = TimeSpan.FromMinutes(30);

    private readonly IServiceProvider _services;
    private readonly ILogger<FileSharePollingService> _logger;
    private readonly FileSharePollingOptions _options;

    public FileSharePollingService(
        IServiceProvider services,
        IOptions<FileSharePollingOptions> options,
        ILogger<FileSharePollingService> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new FileSharePollingOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("File share polling is disabled.");
            return;
        }

        if (!TryValidateConfiguration(out var validationError))
        {
            _logger.LogError("File share polling is enabled but invalid. {ValidationError}", validationError);
            return;
        }

        _logger.LogInformation(
            "File share polling enabled for {WatchedPath}.",
            NormalizePath(_options.WatchedPath!));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await PollOnceAsync(stoppingToken);
                if (result.DiscoveredCount > 0 || result.QueuedCount > 0 || result.QuarantinedCount > 0)
                {
                    _logger.LogInformation(
                        "File share polling cycle completed. Discovered={DiscoveredCount}, queued={QueuedCount}, quarantined={QuarantinedCount}, skipped={SkippedCount}.",
                        result.DiscoveredCount,
                        result.QueuedCount,
                        result.QuarantinedCount,
                        result.SkippedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled file share polling cycle error for {WatchedPath}.", _options.WatchedPath);
            }

            await Task.Delay(
                TimeSpan.FromSeconds(Math.Max(_options.PollIntervalSeconds, 1)),
                stoppingToken);
        }
    }

    public async Task<FileSharePollingCycleResult> PollOnceAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
            return FileSharePollingCycleResult.Disabled;

        if (!TryValidateConfiguration(out var validationError))
        {
            _logger.LogError("Skipping file share polling cycle because configuration is invalid. {ValidationError}", validationError);
            return FileSharePollingCycleResult.InvalidConfiguration;
        }

        string watchedPath = NormalizePath(_options.WatchedPath!);
        string archivePath = NormalizePath(_options.ArchivePath!);
        string errorPath = NormalizePath(_options.ErrorPath!);
        string processingPath = GetProcessingPath(watchedPath);
        string receiptRoot = GetReceiptRoot(archivePath);

        if (!TryEnsureOperationalDirectories(watchedPath, processingPath, archivePath, errorPath, receiptRoot, out var pathError))
        {
            _logger.LogWarning("Skipping file share polling cycle because a required path is unavailable. {PathError}", pathError);
            return FileSharePollingCycleResult.Empty;
        }

        await RecoverStaleProcessingFilesAsync(processingPath, errorPath, ct);
        RecoverStaleClaimFiles(receiptRoot);

        var discoveredFiles = DiscoverCandidateFiles(watchedPath);
        if (discoveredFiles.Count == 0)
            return FileSharePollingCycleResult.Empty;

        int maxFilesPerCycle = _options.MaxFilesPerCycle.GetValueOrDefault(int.MaxValue);
        if (maxFilesPerCycle <= 0)
            maxFilesPerCycle = int.MaxValue;

        var selectedFiles = discoveredFiles.Take(maxFilesPerCycle).ToArray();

        int queuedCount = 0;
        int quarantinedCount = 0;
        int skippedCount = discoveredFiles.Count - selectedFiles.Length;

        foreach (var filePath in selectedFiles)
        {
            ct.ThrowIfCancellationRequested();

            var outcome = await ProcessCandidateAsync(filePath, processingPath, archivePath, errorPath, receiptRoot, ct);
            switch (outcome)
            {
                case CandidateProcessingOutcome.Queued:
                    queuedCount++;
                    break;

                case CandidateProcessingOutcome.Quarantined:
                    quarantinedCount++;
                    break;

                default:
                    skippedCount++;
                    break;
            }
        }

        return new FileSharePollingCycleResult(
            discoveredFiles.Count,
            queuedCount,
            quarantinedCount,
            skippedCount);
    }

    private async Task<CandidateProcessingOutcome> ProcessCandidateAsync(
        string sourcePath,
        string processingPath,
        string archivePath,
        string errorPath,
        string receiptRoot,
        CancellationToken ct)
    {
        FileInfo sourceInfo;
        try
        {
            sourceInfo = new FileInfo(sourcePath);
            if (!sourceInfo.Exists)
                return CandidateProcessingOutcome.Skipped;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unable to inspect polled file {FilePath}.", sourcePath);
            return CandidateProcessingOutcome.Skipped;
        }

        if (!IsFileReady(sourceInfo, out var readinessReason))
        {
            _logger.LogDebug("Skipping polled file {FilePath} because it is not ready. {ReadinessReason}", sourcePath, readinessReason);
            return CandidateProcessingOutcome.Skipped;
        }

        string originalPath = sourceInfo.FullName;
        string originalFileName = sourceInfo.Name;
        string claimedPath;

        try
        {
            claimedPath = ClaimSourceFile(originalPath, originalFileName, processingPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Skipping polled file {FilePath} because another process likely claimed it first.", sourcePath);
            return CandidateProcessingOutcome.Skipped;
        }

        string? fingerprint = null;
        DateOnly? snapshotDate = null;
        string? itsmSource = null;

        try
        {
            var claimedInfo = new FileInfo(claimedPath);
            if (!claimedInfo.Exists)
                return CandidateProcessingOutcome.Skipped;

            if (claimedInfo.Length == 0)
            {
                await QuarantineClaimedFileAsync(
                    claimedPath,
                    originalPath,
                    originalFileName,
                    errorPath,
                    "empty-file",
                    "File length was zero and was not queued for ingest.",
                    null,
                    null,
                    null,
                    null,
                    0,
                    ct);

                return CandidateProcessingOutcome.Quarantined;
            }

            fingerprint = await ComputeFingerprintAsync(claimedPath, ct);

            if (!TryResolveSnapshotDate(originalFileName, out var resolvedSnapshotDate))
            {
                await QuarantineClaimedFileAsync(
                    claimedPath,
                    originalPath,
                    originalFileName,
                    errorPath,
                    "snapshot-date-unresolved",
                    "Snapshot date could not be inferred from the file name.",
                    fingerprint,
                    null,
                    null,
                    null,
                    claimedInfo.Length,
                    ct);

                return CandidateProcessingOutcome.Quarantined;
            }

            snapshotDate = resolvedSnapshotDate;
            itsmSource = ResolveItsmSource(originalFileName);
            if (string.IsNullOrWhiteSpace(itsmSource))
            {
                await QuarantineClaimedFileAsync(
                    claimedPath,
                    originalPath,
                    originalFileName,
                    errorPath,
                    "itsm-source-unresolved",
                    "ITSM source could not be resolved. Configure FileSharePolling:ItsmSource for the watched path.",
                    fingerprint,
                    null,
                    snapshotDate,
                    null,
                    claimedInfo.Length,
                    ct);

                return CandidateProcessingOutcome.Quarantined;
            }

            var claim = TryAcquireFingerprintClaim(
                receiptRoot,
                CreateMetadata(
                    originalPath,
                    originalFileName,
                    fingerprint,
                    itsmSource,
                    snapshotDate,
                    ResolveContentType(originalFileName),
                    claimedInfo.Length,
                    null,
                    null,
                    "claim",
                    null));

            if (claim is null)
            {
                await QuarantineClaimedFileAsync(
                    claimedPath,
                    originalPath,
                    originalFileName,
                    errorPath,
                    "duplicate",
                    "A matching file fingerprint has already been queued or processed.",
                    fingerprint,
                    itsmSource,
                    snapshotDate,
                    null,
                    claimedInfo.Length,
                    ct);

                return CandidateProcessingOutcome.Quarantined;
            }

            try
            {
                BackgroundJob job;
                using (var scope = _services.CreateScope())
                {
                    var submissions = scope.ServiceProvider.GetRequiredService<ISnapshotIngestJobSubmissionService>();
                    await using var stream = File.OpenRead(claimedPath);
                    job = await submissions.QueueAsync(
                        stream,
                        originalFileName,
                        ResolveContentType(originalFileName),
                        itsmSource,
                        snapshotDate.Value,
                        BuildRequestedBy(),
                        ct);
                }

                string archivedPath = MoveClaimedFile(
                    claimedPath,
                    Path.Combine(archivePath, snapshotDate.Value.ToString("yyyyMMdd")),
                    originalFileName);

                var metadata = CreateMetadata(
                    originalPath,
                    originalFileName,
                    fingerprint,
                    itsmSource,
                    snapshotDate,
                    ResolveContentType(originalFileName),
                    claimedInfo.Length,
                    job.Id,
                    archivedPath,
                    "queued",
                    null);

                try
                {
                    await FinalizeFingerprintReceiptAsync(claim, metadata, ct);
                    await WriteSidecarAsync(archivedPath, metadata, ct);
                }
                catch (Exception metadataEx)
                {
                    _logger.LogWarning(
                        metadataEx,
                        "Queued snapshot ingest job {JobId} for polled file {OriginalPath}, but failed to persist archive metadata or duplicate-receipt state.",
                        job.Id,
                        originalPath);
                }

                _logger.LogInformation(
                    "Queued snapshot ingest job {JobId} for polled file {FileName} from {OriginalPath}. Fingerprint={Fingerprint}; SnapshotDate={SnapshotDate}; ItsmSource={ItsmSource}; SizeBytes={SizeBytes}; ArchivedPath={ArchivedPath}",
                    job.Id,
                    originalFileName,
                    originalPath,
                    fingerprint,
                    snapshotDate,
                    itsmSource,
                    claimedInfo.Length,
                    archivedPath);

                return CandidateProcessingOutcome.Queued;
            }
            catch (Exception ex)
            {
                ReleaseFingerprintClaim(claim);

                await QuarantineClaimedFileAsync(
                    claimedPath,
                    originalPath,
                    originalFileName,
                    errorPath,
                    "submission-failed",
                    ex.Message,
                    fingerprint,
                    itsmSource,
                    snapshotDate,
                    null,
                    claimedInfo.Length,
                    ct);

                _logger.LogError(ex, "Failed to queue snapshot ingest job for polled file {OriginalPath}.", originalPath);
                return CandidateProcessingOutcome.Quarantined;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected file share polling error while processing {OriginalPath}.", originalPath);

            if (File.Exists(claimedPath))
            {
                try
                {
                    var claimedInfo = new FileInfo(claimedPath);
                    await QuarantineClaimedFileAsync(
                        claimedPath,
                        originalPath,
                        originalFileName,
                        errorPath,
                        "unexpected-error",
                        ex.Message,
                        fingerprint,
                        itsmSource,
                        snapshotDate,
                        null,
                        claimedInfo.Exists ? claimedInfo.Length : null,
                        ct);
                }
                catch (Exception quarantineEx)
                {
                    _logger.LogError(quarantineEx, "Failed to quarantine polled file {OriginalPath} after an unexpected error.", originalPath);
                }
            }

            return CandidateProcessingOutcome.Quarantined;
        }
    }

    private async Task RecoverStaleProcessingFilesAsync(
        string processingPath,
        string errorPath,
        CancellationToken ct)
    {
        if (!Directory.Exists(processingPath))
            return;

        foreach (var processingFile in Directory.EnumerateFiles(processingPath, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();

            var info = new FileInfo(processingFile);
            if (!info.Exists)
                continue;

            if (DateTime.UtcNow - info.LastWriteTimeUtc < StaleClaimAge)
                continue;

            string? fingerprint = null;
            try
            {
                if (info.Length > 0)
                {
                    fingerprint = await ComputeFingerprintAsync(processingFile, ct);
                    DeleteReceiptClaimFileIfPresent(GetReceiptClaimPath(GetReceiptRoot(NormalizePath(_options.ArchivePath!)), fingerprint));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to compute a fingerprint for stale processing file {ProcessingFile}.", processingFile);
            }

            await QuarantineClaimedFileAsync(
                processingFile,
                processingFile,
                info.Name,
                errorPath,
                "stale-processing",
                "The file was left in the processing folder by an earlier interrupted poll cycle.",
                fingerprint,
                null,
                null,
                null,
                info.Length,
                ct);

            _logger.LogWarning("Moved stale processing file {ProcessingFile} to quarantine.", processingFile);
        }
    }

    private void RecoverStaleClaimFiles(string receiptRoot)
    {
        if (!Directory.Exists(receiptRoot))
            return;

        foreach (var claimFile in Directory.EnumerateFiles(receiptRoot, "*.claim", SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(claimFile);
            if (!info.Exists)
                continue;

            if (DateTime.UtcNow - info.LastWriteTimeUtc < StaleClaimAge)
                continue;

            try
            {
                info.Delete();
                _logger.LogWarning("Removed stale fingerprint claim file {ClaimFile}.", claimFile);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Unable to remove stale fingerprint claim file {ClaimFile}.", claimFile);
            }
        }
    }

    private IReadOnlyList<string> DiscoverCandidateFiles(string watchedPath)
    {
        try
        {
            var files = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var pattern in GetEffectivePatterns())
            {
                foreach (var filePath in Directory.EnumerateFiles(watchedPath, pattern, SearchOption.TopDirectoryOnly))
                {
                    var info = new FileInfo(filePath);
                    if (!info.Exists)
                        continue;

                    files[info.FullName] = info;
                }
            }

            return files.Values
                .OrderBy(file => file.LastWriteTimeUtc)
                .ThenBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .Select(file => file.FullName)
                .ToArray();
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Unable to enumerate watched path {WatchedPath}.", watchedPath);
            return Array.Empty<string>();
        }
    }

    private bool IsFileReady(FileInfo info, out string reason)
    {
        if (!info.Exists)
        {
            reason = "File no longer exists.";
            return false;
        }

        int stableAgeSeconds = Math.Max(_options.StableFileAgeSeconds, 0);
        if (stableAgeSeconds > 0 && DateTime.UtcNow - info.LastWriteTimeUtc < TimeSpan.FromSeconds(stableAgeSeconds))
        {
            reason = $"File was modified less than {stableAgeSeconds} seconds ago.";
            return false;
        }

        try
        {
            using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.None);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            reason = ex.Message;
            return false;
        }
    }

    private string ClaimSourceFile(string sourcePath, string originalFileName, string processingPath)
    {
        Directory.CreateDirectory(processingPath);

        string claimedPath = BuildUniqueFilePath(processingPath, originalFileName);
        File.Move(sourcePath, claimedPath);
        return claimedPath;
    }

    private string MoveClaimedFile(string claimedPath, string destinationDirectory, string originalFileName)
    {
        Directory.CreateDirectory(destinationDirectory);

        string destinationPath = BuildUniqueFilePath(destinationDirectory, originalFileName);

        try
        {
            File.Move(claimedPath, destinationPath);
        }
        catch (IOException)
        {
            File.Copy(claimedPath, destinationPath, overwrite: false);
            File.Delete(claimedPath);
        }

        return destinationPath;
    }

    private FingerprintClaim? TryAcquireFingerprintClaim(string receiptRoot, FileSharePollingMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata.Fingerprint))
            return null;

        string receiptPath = GetReceiptPath(receiptRoot, metadata.Fingerprint);
        if (File.Exists(receiptPath))
            return null;

        string claimPath = GetReceiptClaimPath(receiptRoot, metadata.Fingerprint);
        var claimInfo = new FileInfo(claimPath);
        if (claimInfo.Exists)
        {
            if (DateTime.UtcNow - claimInfo.LastWriteTimeUtc > StaleClaimAge)
            {
                DeleteReceiptClaimFileIfPresent(claimPath);
            }
            else
            {
                return null;
            }
        }

        try
        {
            Directory.CreateDirectory(receiptRoot);
            using var stream = new FileStream(claimPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(stream);
            writer.Write(JsonSerializer.Serialize(metadata, MetadataJsonOptions));
            writer.Flush();
            return new FingerprintClaim(claimPath, receiptPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Unable to create fingerprint claim file {ClaimPath}.", claimPath);
            return null;
        }
    }

    private async Task FinalizeFingerprintReceiptAsync(
        FingerprintClaim claim,
        FileSharePollingMetadata metadata,
        CancellationToken ct)
    {
        string tempPath = claim.ReceiptPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(metadata, MetadataJsonOptions), ct);

        if (File.Exists(claim.ReceiptPath))
        {
            File.Delete(claim.ReceiptPath);
        }

        File.Move(tempPath, claim.ReceiptPath);
        DeleteReceiptClaimFileIfPresent(claim.ClaimPath);
    }

    private void ReleaseFingerprintClaim(FingerprintClaim claim)
    {
        DeleteReceiptClaimFileIfPresent(claim.ClaimPath);
    }

    private async Task<string> QuarantineClaimedFileAsync(
        string claimedPath,
        string originalPath,
        string originalFileName,
        string errorPath,
        string reason,
        string error,
        string? fingerprint,
        string? itsmSource,
        DateOnly? snapshotDate,
        Guid? jobId,
        long? sizeBytes,
        CancellationToken ct)
    {
        string quarantinedPath = MoveClaimedFile(
            claimedPath,
            Path.Combine(errorPath, SanitizePathSegment(reason)),
            originalFileName);

        var metadata = CreateMetadata(
            originalPath,
            originalFileName,
            fingerprint,
            itsmSource,
            snapshotDate,
            ResolveContentType(originalFileName),
            sizeBytes,
            jobId,
            quarantinedPath,
            reason,
            error);

        await WriteSidecarAsync(quarantinedPath, metadata, ct);

        _logger.LogWarning(
            "Moved polled file {OriginalPath} to quarantine at {QuarantinedPath}. Reason={Reason}; Fingerprint={Fingerprint}; SnapshotDate={SnapshotDate}; ItsmSource={ItsmSource}",
            originalPath,
            quarantinedPath,
            reason,
            fingerprint,
            snapshotDate,
            itsmSource);

        return quarantinedPath;
    }

    private async Task WriteSidecarAsync(string targetPath, FileSharePollingMetadata metadata, CancellationToken ct)
    {
        string sidecarPath = targetPath + ".metadata.json";
        await File.WriteAllTextAsync(sidecarPath, JsonSerializer.Serialize(metadata, MetadataJsonOptions), ct);
    }

    private FileSharePollingMetadata CreateMetadata(
        string originalPath,
        string fileName,
        string? fingerprint,
        string? itsmSource,
        DateOnly? snapshotDate,
        string contentType,
        long? sizeBytes,
        Guid? jobId,
        string? finalPath,
        string disposition,
        string? error)
    {
        return new FileSharePollingMetadata(
            originalPath,
            fileName,
            fingerprint,
            itsmSource,
            snapshotDate?.ToString("yyyy-MM-dd"),
            contentType,
            sizeBytes,
            jobId,
            finalPath,
            disposition,
            error,
            DateTime.UtcNow,
            BuildRequestedBy(),
            Environment.MachineName);
    }

    private bool TryEnsureOperationalDirectories(
        string watchedPath,
        string processingPath,
        string archivePath,
        string errorPath,
        string receiptRoot,
        out string? error)
    {
        error = null;

        try
        {
            if (!Directory.Exists(watchedPath))
            {
                error = $"Watched path '{watchedPath}' does not exist or is unavailable.";
                return false;
            }

            if (!Directory.Exists(archivePath))
            {
                error = $"Archive path '{archivePath}' does not exist or is unavailable.";
                return false;
            }

            if (!Directory.Exists(errorPath))
            {
                error = $"Error path '{errorPath}' does not exist or is unavailable.";
                return false;
            }

            Directory.CreateDirectory(processingPath);
            Directory.CreateDirectory(receiptRoot);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    private bool TryValidateConfiguration(out string? validationError)
    {
        validationError = null;

        if (string.IsNullOrWhiteSpace(_options.WatchedPath))
        {
            validationError = "FileSharePolling:WatchedPath is required when polling is enabled.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.ArchivePath))
        {
            validationError = "FileSharePolling:ArchivePath is required when polling is enabled.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.ErrorPath))
        {
            validationError = "FileSharePolling:ErrorPath is required when polling is enabled.";
            return false;
        }

        string watchedPath = NormalizePath(_options.WatchedPath);
        string archivePath = NormalizePath(_options.ArchivePath);
        string errorPath = NormalizePath(_options.ErrorPath);

        if (string.Equals(watchedPath, archivePath, StringComparison.OrdinalIgnoreCase))
        {
            validationError = "WatchedPath and ArchivePath must be different.";
            return false;
        }

        if (string.Equals(watchedPath, errorPath, StringComparison.OrdinalIgnoreCase))
        {
            validationError = "WatchedPath and ErrorPath must be different.";
            return false;
        }

        if (GetEffectivePatterns().Count == 0)
        {
            validationError = "At least one FileSharePolling:FilePatterns entry is required.";
            return false;
        }

        return true;
    }

    private IReadOnlyList<string> GetEffectivePatterns()
    {
        var configuredPatterns = (_options.FilePatterns ?? Array.Empty<string>())
            .Select(pattern => pattern?.Trim())
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return configuredPatterns.Length == 0
            ? ["*.csv", "*.json"]
            : configuredPatterns;
    }

    private static string GetProcessingPath(string watchedPath)
        => Path.Combine(watchedPath, ".upms-processing");

    private static string GetReceiptRoot(string archivePath)
        => Path.Combine(archivePath, ".upms-receipts");

    private static string GetReceiptPath(string receiptRoot, string fingerprint)
        => Path.Combine(receiptRoot, $"{fingerprint}.json");

    private static string GetReceiptClaimPath(string receiptRoot, string fingerprint)
        => Path.Combine(receiptRoot, $"{fingerprint}.claim");

    private static void DeleteReceiptClaimFileIfPresent(string claimPath)
    {
        try
        {
            if (File.Exists(claimPath))
                File.Delete(claimPath);
        }
        catch
        {
            // Best-effort cleanup of transient fingerprint claim files.
        }
    }

    private string ResolveItsmSource(string fileName)
    {
        _ = fileName;
        return string.IsNullOrWhiteSpace(_options.ItsmSource)
            ? string.Empty
            : _options.ItsmSource.Trim();
    }

    private static bool TryResolveSnapshotDate(string fileName, out DateOnly snapshotDate)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);

        Match match = SeparatedDateRegex().Match(stem);
        if (match.Success)
        {
            return TryBuildDate(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, out snapshotDate);
        }

        match = CompactDateRegex().Match(stem);
        if (match.Success)
        {
            return TryBuildDate(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, out snapshotDate);
        }

        snapshotDate = default;
        return false;
    }

    private static bool TryBuildDate(string yearText, string monthText, string dayText, out DateOnly snapshotDate)
    {
        snapshotDate = default;

        if (!int.TryParse(yearText, out var year)
            || !int.TryParse(monthText, out var month)
            || !int.TryParse(dayText, out var day))
        {
            return false;
        }

        try
        {
            snapshotDate = new DateOnly(year, month, day);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
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

    private static string BuildUniqueFilePath(string directory, string fileName)
    {
        string safeFileName = Path.GetFileName(fileName);
        return Path.Combine(directory, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}_{safeFileName}");
    }

    private static string NormalizePath(string path)
        => Path.GetFullPath(path.Trim());

    private static string SanitizePathSegment(string value)
    {
        var chars = value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray();

        var sanitized = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "error" : sanitized;
    }

    private static string BuildRequestedBy()
        => $"file-share-poller@{Environment.MachineName}";

    private static async Task<string> ComputeFingerprintAsync(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    [GeneratedRegex(@"((?:19|20)\d{2})[-_.](\d{2})[-_.](\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatedDateRegex();

    [GeneratedRegex(@"((?:19|20)\d{2})(\d{2})(\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex CompactDateRegex();

    private sealed record FingerprintClaim(string ClaimPath, string ReceiptPath);

    private sealed record FileSharePollingMetadata(
        string OriginalPath,
        string FileName,
        string? Fingerprint,
        string? ItsmSource,
        string? SnapshotDate,
        string ContentType,
        long? SizeBytes,
        Guid? JobId,
        string? FinalPath,
        string Disposition,
        string? Error,
        DateTime ProcessedAtUtc,
        string RequestedBy,
        string MachineName);

    private enum CandidateProcessingOutcome
    {
        Skipped,
        Queued,
        Quarantined
    }
}
