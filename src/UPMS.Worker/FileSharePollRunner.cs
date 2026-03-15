namespace UPMS.Worker;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UPMS.Data;
using UPMS.Data.Jobs;

public interface IFileSharePollRunner
{
    Task<FileSharePollingCycleResult> PollOnceAsync(FileSharePollingSource source, CancellationToken ct);
}

public sealed partial class FileSharePollRunner : IFileSharePollRunner
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly TimeSpan StaleClaimAge = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FileSharePollRunner> _logger;
    private readonly FileSharePollingOptions _options;

    public FileSharePollRunner(
        IServiceScopeFactory scopeFactory,
        IOptions<FileSharePollingOptions> options,
        ILogger<FileSharePollRunner> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new FileSharePollingOptions();
    }

    public async Task<FileSharePollingCycleResult> PollOnceAsync(FileSharePollingSource source, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!TryValidateConfiguration(source, out var validationError))
        {
            _logger.LogError(
                "Skipping file share polling cycle for source {SourceId} ({SourceName}) because configuration is invalid. {ValidationError}",
                source.Id,
                source.Name,
                validationError);

            return FileSharePollingCycleResult.InvalidConfiguration(validationError!);
        }

        string watchedPath = NormalizePath(source.WatchedPath);
        string archivePath = NormalizePath(source.ArchivePath);
        string errorPath = NormalizePath(source.ErrorPath);
        string processingPath = GetProcessingPath(watchedPath, source.Id);
        string receiptRoot = GetReceiptRoot(archivePath, source.Id);

        if (!TryEnsureOperationalDirectories(watchedPath, processingPath, archivePath, errorPath, receiptRoot, out var pathError))
        {
            _logger.LogWarning(
                "Skipping file share polling cycle for source {SourceId} ({SourceName}) because a required path is unavailable. {PathError}",
                source.Id,
                source.Name,
                pathError);

            return FileSharePollingCycleResult.PathUnavailable(pathError!);
        }

        await RecoverStaleProcessingFilesAsync(source, processingPath, errorPath, receiptRoot, ct);
        RecoverStaleClaimFiles(receiptRoot);

        var discoveredFiles = DiscoverCandidateFiles(source, watchedPath);
        if (discoveredFiles.Count == 0)
            return FileSharePollingCycleResult.Empty;

        int maxFilesPerCycle = GetEffectiveMaxFilesPerCycle(source);
        var selectedFiles = discoveredFiles.Take(maxFilesPerCycle).ToArray();

        int queuedCount = 0;
        int quarantinedCount = 0;
        int skippedCount = discoveredFiles.Count - selectedFiles.Length;

        foreach (var filePath in selectedFiles)
        {
            ct.ThrowIfCancellationRequested();

            var outcome = await ProcessCandidateAsync(source, filePath, processingPath, archivePath, errorPath, receiptRoot, ct);
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

        return FileSharePollingCycleResult.Completed(
            discoveredFiles.Count,
            queuedCount,
            quarantinedCount,
            skippedCount);
    }

    private async Task<CandidateProcessingOutcome> ProcessCandidateAsync(
        FileSharePollingSource source,
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
            _logger.LogWarning(ex, "Unable to inspect polled file {FilePath} for source {SourceId} ({SourceName}).", sourcePath, source.Id, source.Name);
            return CandidateProcessingOutcome.Skipped;
        }

        if (!IsFileReady(sourceInfo, source.StableFileAgeSeconds, out var readinessReason))
        {
            _logger.LogDebug(
                "Skipping polled file {FilePath} for source {SourceId} ({SourceName}) because it is not ready. {ReadinessReason}",
                sourcePath,
                source.Id,
                source.Name,
                readinessReason);
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

        try
        {
            var claimedInfo = new FileInfo(claimedPath);
            if (!claimedInfo.Exists)
                return CandidateProcessingOutcome.Skipped;

            if (claimedInfo.Length == 0)
            {
                await QuarantineClaimedFileAsync(
                    source,
                    claimedPath,
                    originalPath,
                    originalFileName,
                    errorPath,
                    "empty-file",
                    "File length was zero and was not queued for ingest.",
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
                    source,
                    claimedPath,
                    originalPath,
                    originalFileName,
                    errorPath,
                    "snapshot-date-unresolved",
                    "Snapshot date could not be inferred from the file name.",
                    fingerprint,
                    null,
                    null,
                    claimedInfo.Length,
                    ct);

                return CandidateProcessingOutcome.Quarantined;
            }

            snapshotDate = resolvedSnapshotDate;

            var claim = TryAcquireFingerprintClaim(
                receiptRoot,
                CreateMetadata(
                    source,
                    originalPath,
                    originalFileName,
                    fingerprint,
                    source.ItsmSource,
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
                    source,
                    claimedPath,
                    originalPath,
                    originalFileName,
                    errorPath,
                    "duplicate",
                    "A matching file fingerprint has already been queued or processed for this polling source.",
                    fingerprint,
                    snapshotDate,
                    null,
                    claimedInfo.Length,
                    ct);

                return CandidateProcessingOutcome.Quarantined;
            }

            try
            {
                BackgroundJob job;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var submissions = scope.ServiceProvider.GetRequiredService<ISnapshotIngestJobSubmissionService>();
                    await using var stream = File.OpenRead(claimedPath);
                    job = await submissions.QueueAsync(
                        stream,
                        originalFileName,
                        ResolveContentType(originalFileName),
                        source.ItsmSource,
                        snapshotDate.Value,
                        BuildRequestedBy(),
                        ct);
                }

                string archivedPath = MoveClaimedFile(
                    claimedPath,
                    Path.Combine(archivePath, snapshotDate.Value.ToString("yyyyMMdd")),
                    originalFileName);

                var metadata = CreateMetadata(
                    source,
                    originalPath,
                    originalFileName,
                    fingerprint,
                    source.ItsmSource,
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
                    "Queued snapshot ingest job {JobId} for polled file {FileName} from {OriginalPath}. SourceId={SourceId}; SourceName={SourceName}; Fingerprint={Fingerprint}; SnapshotDate={SnapshotDate}; ItsmSource={ItsmSource}; SizeBytes={SizeBytes}; ArchivedPath={ArchivedPath}",
                    job.Id,
                    originalFileName,
                    originalPath,
                    source.Id,
                    source.Name,
                    fingerprint,
                    snapshotDate,
                    source.ItsmSource,
                    claimedInfo.Length,
                    archivedPath);

                return CandidateProcessingOutcome.Queued;
            }
            catch (Exception ex)
            {
                ReleaseFingerprintClaim(claim);

                await QuarantineClaimedFileAsync(
                    source,
                    claimedPath,
                    originalPath,
                    originalFileName,
                    errorPath,
                    "submission-failed",
                    ex.Message,
                    fingerprint,
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
            _logger.LogError(ex, "Unexpected file share polling error while processing {OriginalPath} for source {SourceId} ({SourceName}).", originalPath, source.Id, source.Name);

            if (File.Exists(claimedPath))
            {
                try
                {
                    var claimedInfo = new FileInfo(claimedPath);
                    await QuarantineClaimedFileAsync(
                        source,
                        claimedPath,
                        originalPath,
                        originalFileName,
                        errorPath,
                        "unexpected-error",
                        ex.Message,
                        fingerprint,
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
        FileSharePollingSource source,
        string processingPath,
        string errorPath,
        string receiptRoot,
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
                    DeleteReceiptClaimFileIfPresent(GetReceiptClaimPath(receiptRoot, fingerprint));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to compute a fingerprint for stale processing file {ProcessingFile}.", processingFile);
            }

            await QuarantineClaimedFileAsync(
                source,
                processingFile,
                processingFile,
                info.Name,
                errorPath,
                "stale-processing",
                "The file was left in the processing folder by an earlier interrupted poll cycle.",
                fingerprint,
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

    private IReadOnlyList<string> DiscoverCandidateFiles(FileSharePollingSource source, string watchedPath)
    {
        try
        {
            var files = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var pattern in GetEffectivePatterns(source))
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
            _logger.LogWarning(ex, "Unable to enumerate watched path {WatchedPath} for source {SourceId} ({SourceName}).", watchedPath, source.Id, source.Name);
            return Array.Empty<string>();
        }
    }

    private static bool IsFileReady(FileInfo info, int stableAgeSeconds, out string reason)
    {
        if (!info.Exists)
        {
            reason = "File no longer exists.";
            return false;
        }

        int boundedStableAgeSeconds = Math.Max(stableAgeSeconds, 0);
        if (boundedStableAgeSeconds > 0 && DateTime.UtcNow - info.LastWriteTimeUtc < TimeSpan.FromSeconds(boundedStableAgeSeconds))
        {
            reason = $"File was modified less than {boundedStableAgeSeconds} seconds ago.";
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

    private static string ClaimSourceFile(string sourcePath, string originalFileName, string processingPath)
    {
        Directory.CreateDirectory(processingPath);

        string claimedPath = BuildUniqueFilePath(processingPath, originalFileName);
        File.Move(sourcePath, claimedPath);
        return claimedPath;
    }

    private static string MoveClaimedFile(string claimedPath, string destinationDirectory, string originalFileName)
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

    private static async Task FinalizeFingerprintReceiptAsync(
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

    private static void ReleaseFingerprintClaim(FingerprintClaim claim)
    {
        DeleteReceiptClaimFileIfPresent(claim.ClaimPath);
    }

    private async Task<string> QuarantineClaimedFileAsync(
        FileSharePollingSource source,
        string claimedPath,
        string originalPath,
        string originalFileName,
        string errorPath,
        string reason,
        string error,
        string? fingerprint,
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
            source,
            originalPath,
            originalFileName,
            fingerprint,
            source.ItsmSource,
            snapshotDate,
            ResolveContentType(originalFileName),
            sizeBytes,
            jobId,
            quarantinedPath,
            reason,
            error);

        await WriteSidecarAsync(quarantinedPath, metadata, ct);

        _logger.LogWarning(
            "Moved polled file {OriginalPath} to quarantine at {QuarantinedPath}. SourceId={SourceId}; SourceName={SourceName}; Reason={Reason}; Fingerprint={Fingerprint}; SnapshotDate={SnapshotDate}; ItsmSource={ItsmSource}",
            originalPath,
            quarantinedPath,
            source.Id,
            source.Name,
            reason,
            fingerprint,
            snapshotDate,
            source.ItsmSource);

        return quarantinedPath;
    }

    private static async Task WriteSidecarAsync(string targetPath, FileSharePollingMetadata metadata, CancellationToken ct)
    {
        string sidecarPath = targetPath + ".metadata.json";
        await File.WriteAllTextAsync(sidecarPath, JsonSerializer.Serialize(metadata, MetadataJsonOptions), ct);
    }

    private FileSharePollingMetadata CreateMetadata(
        FileSharePollingSource source,
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
            source.Id,
            source.Name,
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

    private static bool TryEnsureOperationalDirectories(
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

            Directory.CreateDirectory(archivePath);
            Directory.CreateDirectory(errorPath);
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

    private static bool TryValidateConfiguration(FileSharePollingSource source, out string? validationError)
    {
        validationError = null;

        if (string.IsNullOrWhiteSpace(source.WatchedPath))
        {
            validationError = "WatchedPath is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source.ArchivePath))
        {
            validationError = "ArchivePath is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source.ErrorPath))
        {
            validationError = "ErrorPath is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source.ItsmSource))
        {
            validationError = "ItsmSource is required.";
            return false;
        }

        string watchedPath = NormalizePath(source.WatchedPath);
        string archivePath = NormalizePath(source.ArchivePath);
        string errorPath = NormalizePath(source.ErrorPath);

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

        if (string.Equals(archivePath, errorPath, StringComparison.OrdinalIgnoreCase))
        {
            validationError = "ArchivePath and ErrorPath must be different.";
            return false;
        }

        if (GetEffectivePatterns(source).Count == 0)
        {
            validationError = "At least one file pattern is required.";
            return false;
        }

        if (source.StableFileAgeSeconds < 0)
        {
            validationError = "StableFileAgeSeconds must be zero or greater.";
            return false;
        }

        return true;
    }

    private int GetEffectiveMaxFilesPerCycle(FileSharePollingSource source)
    {
        int configuredLimit = source.MaxFilesPerCycle.GetValueOrDefault(int.MaxValue);
        if (configuredLimit <= 0)
            configuredLimit = int.MaxValue;

        int cap = Math.Max(_options.MaxFilesPerCycleCap, 1);
        return Math.Min(configuredLimit, cap);
    }

    private static IReadOnlyList<string> GetEffectivePatterns(FileSharePollingSource source)
        => source.GetFilePatterns();

    private static string GetProcessingPath(string watchedPath, Guid sourceId)
        => Path.Combine(watchedPath, ".upms-processing", sourceId.ToString("N"));

    private static string GetReceiptRoot(string archivePath, Guid sourceId)
        => Path.Combine(archivePath, ".upms-receipts", sourceId.ToString("N"));

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
        Guid SourceId,
        string SourceName,
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
