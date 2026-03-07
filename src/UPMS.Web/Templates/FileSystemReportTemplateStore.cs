namespace UPMS.Web.Templates;

using System.Text.Json;

public sealed class FileSystemReportTemplateStore : IReportTemplateStore
{
    private const string MetadataFileName = "template.meta.json";
    private readonly string _storagePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileSystemReportTemplateStore(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Template storage path is required.", nameof(storagePath));

        _storagePath = storagePath;
        Directory.CreateDirectory(_storagePath);
    }

    public IReadOnlyList<ReportTemplateMetadata> GetAllTemplates()
    {
        if (!Directory.Exists(_storagePath))
            return Array.Empty<ReportTemplateMetadata>();

        return Directory.GetDirectories(_storagePath)
            .Select(ReadMetadata)
            .Where(static m => m is not null)
            .Cast<ReportTemplateMetadata>()
            .OrderBy(m => m.Kind)
            .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ReportTemplateMetadata> GetTemplatesByKind(ReportTemplateKind kind) =>
        GetAllTemplates()
            .Where(t => t.Kind == kind)
            .ToList();

    public ReportTemplateMetadata? GetTemplateById(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return null;

        var directory = Path.Combine(_storagePath, templateId.Trim());
        return ReadMetadata(directory);
    }

    public async Task<StoredReportTemplate?> GetTemplateContentAsync(string templateId, CancellationToken ct = default)
    {
        var metadata = GetTemplateById(templateId);
        if (metadata is null)
            return null;

        var filePath = Path.Combine(_storagePath, metadata.Id, metadata.FileName);
        if (!File.Exists(filePath))
            return null;

        await using var stream = File.OpenRead(filePath);
        using MemoryStream ms = new();
        await stream.CopyToAsync(ms, ct);

        return new StoredReportTemplate
        {
            Metadata = metadata,
            FileContent = ms.ToArray()
        };
    }

    public async Task<ReportTemplateMetadata> SaveAsync(ReportTemplateUploadRequest request, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);

        var extension = ReportTemplateContentTypeMapper.Normalize(Path.GetExtension(request.OriginalFileName));
        if (!ReportTemplateContentTypeMapper.IsSupported(extension))
        {
            throw new InvalidOperationException(
                $"Unsupported template type '{extension}'. Supported types: {string.Join(", ", ReportTemplateContentTypeMapper.SupportedExtensions)}");
        }

        var templateId = CreateTemplateId(request.DisplayName);
        var safeFileName = $"template{extension}";
        var templateDirectory = Path.Combine(_storagePath, templateId);
        var templateFilePath = Path.Combine(templateDirectory, safeFileName);
        var metadataFilePath = Path.Combine(templateDirectory, MetadataFileName);

        var metadata = new ReportTemplateMetadata
        {
            Id = templateId,
            DisplayName = request.DisplayName.Trim(),
            Kind = request.Kind,
            Description = NormalizeNullable(request.Description),
            SubjectTemplate = NormalizeNullable(request.SubjectTemplate),
            FileName = safeFileName,
            Extension = extension,
            ContentType = ReportTemplateContentTypeMapper.GetContentType(extension),
            UploadedAt = DateTimeOffset.UtcNow,
            UploadedBy = string.IsNullOrWhiteSpace(request.UploadedBy) ? "unknown" : request.UploadedBy.Trim()
        };

        await _writeLock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(templateDirectory);

            await using (var fileStream = File.Create(templateFilePath))
            {
                await content.CopyToAsync(fileStream, ct);
            }

            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(metadataFilePath, json, ct);
        }
        finally
        {
            _writeLock.Release();
        }

        return metadata;
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string CreateTemplateId(string displayName)
    {
        var safe = new string((displayName ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(safe))
            safe = "template";

        return $"{safe}-{Guid.NewGuid():N}";
    }

    private static ReportTemplateMetadata? ReadMetadata(string directory)
    {
        var metadataPath = Path.Combine(directory, MetadataFileName);
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize<ReportTemplateMetadata>(json);
        }
        catch
        {
            return null;
        }
    }
}
