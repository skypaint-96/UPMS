namespace UPMS.Web;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using UPMS.Web.Plugins;

/// <summary>
/// Stores generated file reports in a temporary, process-local cache so they can be
/// downloaded over a normal HTTP request after being generated from a Blazor circuit.
/// </summary>
public sealed class ReportDownloadStore
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, PendingReportDownload> _downloads =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Stores a generated file report and returns a download token.
    /// </summary>
    public string Store(ReportResult result, TimeSpan? lifetime = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.FileContent is null || result.FileContent.Length == 0)
        {
            throw new InvalidOperationException("The report result does not contain any file content to download.");
        }

        RemoveExpiredEntries();

        string token = Guid.NewGuid().ToString("N");
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(lifetime ?? DefaultLifetime);

        _downloads[token] = new PendingReportDownload(
            result.FileName ?? "report",
            result.ContentType ?? "application/octet-stream",
            result.FileContent.ToArray(),
            expiresAt);

        return token;
    }

    /// <summary>
    /// Attempts to get a pending download from the store.
    /// Expired entries are removed automatically.
    /// </summary>
    public bool TryGet(string token, [NotNullWhen(true)] out PendingReportDownload? download)
    {
        download = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        RemoveExpiredEntries();

        if (!_downloads.TryGetValue(token, out PendingReportDownload? candidate))
        {
            return false;
        }

        if (candidate.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        download = candidate;
        return true;
    }

    private void RemoveExpiredEntries()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (var entry in _downloads)
        {
            if (entry.Value.ExpiresAtUtc <= now)
            {
                _downloads.TryRemove(entry.Key, out _);
            }
        }
    }
}

public sealed record PendingReportDownload(
    string FileName,
    string ContentType,
    byte[] FileContent,
    DateTimeOffset ExpiresAtUtc);
