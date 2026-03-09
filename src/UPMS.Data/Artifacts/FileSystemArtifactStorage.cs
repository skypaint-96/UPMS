namespace UPMS.Data.Artifacts;

using Microsoft.Extensions.Options;

public class FileSystemArtifactStorage : IArtifactStorage
{
    private readonly string _rootPath;

    public FileSystemArtifactStorage(IOptions<ArtifactStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _rootPath = string.IsNullOrWhiteSpace(options.Value.RootPath)
            ? "/var/lib/upms"
            : options.Value.RootPath.Trim();

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredArtifact> SaveAsync(
        string category,
        string fileName,
        Stream content,
        string? contentType,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        string safeCategory = SanitizePathPart(category);
        string safeFileName = SanitizeFileName(fileName);
        string directory = Path.Combine(_rootPath, safeCategory, DateTime.UtcNow.ToString("yyyyMMdd"));
        Directory.CreateDirectory(directory);

        string uniquePrefix = Guid.NewGuid().ToString("N");
        string absolutePath = Path.Combine(directory, $"{uniquePrefix}_{safeFileName}");

        await using (var fileStream = File.Create(absolutePath))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        var info = new FileInfo(absolutePath);
        string relativePath = Path.GetRelativePath(_rootPath, absolutePath).Replace('\\', '/');

        return new StoredArtifact(relativePath, absolutePath, safeFileName, contentType, info.Length);
    }

    public async Task<StoredArtifact> SaveBytesAsync(
        string category,
        string fileName,
        byte[] content,
        string? contentType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        await using var stream = new MemoryStream(content, writable: false);
        return await SaveAsync(category, fileName, stream, contentType, ct);
    }

    public bool Exists(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        return File.Exists(GetAbsolutePath(relativePath));
    }

    public string GetAbsolutePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string combined = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        string fullRoot = Path.GetFullPath(_rootPath);

        if (!combined.StartsWith(fullRoot, StringComparison.Ordinal))
            throw new InvalidOperationException("Resolved artifact path escaped the configured storage root.");

        return combined;
    }

    public Stream OpenRead(string relativePath)
    {
        string absolutePath = GetAbsolutePath(relativePath);
        return File.OpenRead(absolutePath);
    }

    private static string SanitizePathPart(string value)
    {
        var chars = value
            .Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray();

        var sanitized = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(sanitized) ? "artifacts" : sanitized;
    }

    private static string SanitizeFileName(string value)
    {
        var fileName = Path.GetFileName(value.Trim());
        var chars = fileName
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();

        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "artifact.bin" : sanitized;
    }
}
