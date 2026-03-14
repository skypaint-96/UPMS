namespace UPMS.Reporting.Templates;

using System.Text.Json;

public sealed class FileSystemReportTemplateStore : IReportTemplateStore
{
    private const string MetadataFileName = "template.meta.json";
    private readonly string _storagePath;
    private readonly ReportTemplateTypeRegistry _typeRegistry;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileSystemReportTemplateStore(string storagePath, ReportTemplateTypeRegistry? typeRegistry = null)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Template storage path is required.", nameof(storagePath));

        _storagePath = storagePath;
        _typeRegistry = typeRegistry ?? new ReportTemplateTypeRegistry([new DefaultReportTemplateTypeProvider()]);
        Directory.CreateDirectory(_storagePath);
    }

    public IReadOnlyList<ReportTemplateMetadata> GetAllTemplates()
    {
        if (!Directory.Exists(_storagePath))
            return Array.Empty<ReportTemplateMetadata>();

        return Directory.GetDirectories(_storagePath)
            .Select(ReadMetadata)
            .Where(static metadata => metadata is not null)
            .Cast<ReportTemplateMetadata>()
            .OrderBy(metadata => metadata.Kind)
            .ThenBy(metadata => metadata.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<ReportTemplateMetadata> GetTemplatesByKind(ReportTemplateKind kind) =>
        GetAllTemplates()
            .Where(template => template.Kind == kind)
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

    public Task<ReportTemplateMetadata> SaveAsync(ReportTemplateUploadRequest request, Stream content, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return SaveInternalAsync(request, content, overwriteTemplateId: false, ct);
    }

    public async Task<ReportTemplateMetadata> UpdateAsync(string templateId, ReportTemplateUploadRequest request, Stream? content, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentNullException.ThrowIfNull(request);

        var existing = GetTemplateById(templateId)
            ?? throw new KeyNotFoundException($"Report template '{templateId}' was not found.");

        var effectiveExtension = content is null
            ? existing.Extension
            : ReportTemplateContentTypeMapper.Normalize(Path.GetExtension(request.OriginalFileName));

        var resolvedType = ResolveTemplateType(request, effectiveExtension, fallbackKind: existing.Kind, fallbackTypeId: existing.TemplateTypeId);
        var safeFileName = $"template{effectiveExtension}";
        var templateDirectory = Path.Combine(_storagePath, existing.Id);
        var templateFilePath = Path.Combine(templateDirectory, safeFileName);
        var metadataFilePath = Path.Combine(templateDirectory, MetadataFileName);

        var metadata = new ReportTemplateMetadata
        {
            Id = existing.Id,
            TemplateTypeId = resolvedType.TypeId,
            DisplayName = NormalizeRequired(request.DisplayName, nameof(request.DisplayName)),
            Kind = resolvedType.Kind,
            Description = NormalizeNullable(request.Description),
            SubjectTemplate = NormalizeNullable(request.SubjectTemplate),
            Scope = ReportTemplateScopeEvaluator.Normalize(request.Scope ?? existing.Scope),
            FileName = safeFileName,
            Extension = effectiveExtension,
            ContentType = resolvedType.ContentType,
            UploadedAt = existing.UploadedAt,
            UploadedBy = string.IsNullOrWhiteSpace(existing.UploadedBy) ? "unknown" : existing.UploadedBy,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = NormalizeUser(request.UploadedBy),
            IsStarterTemplate = existing.IsStarterTemplate || request.IsStarterTemplate
        };

        await _writeLock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(templateDirectory);

            if (content is not null)
            {
                foreach (var path in Directory.GetFiles(templateDirectory))
                {
                    if (string.Equals(Path.GetFileName(path), MetadataFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    File.Delete(path);
                }

                if (content.CanSeek)
                    content.Position = 0;

                await using var fileStream = File.Create(templateFilePath);
                await content.CopyToAsync(fileStream, ct);
            }
            else if (!string.Equals(existing.FileName, safeFileName, StringComparison.OrdinalIgnoreCase))
            {
                var currentPath = Path.Combine(templateDirectory, existing.FileName);
                if (File.Exists(currentPath))
                {
                    File.Move(currentPath, templateFilePath, overwrite: true);
                }
            }

            await WriteMetadataAsync(metadataFilePath, metadata, ct);
        }
        finally
        {
            _writeLock.Release();
        }

        return metadata;
    }

    public async Task<bool> DeleteAsync(string templateId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);

        var directory = Path.Combine(_storagePath, templateId.Trim());
        if (!Directory.Exists(directory))
            return false;

        await _writeLock.WaitAsync(ct);
        try
        {
            if (!Directory.Exists(directory))
                return false;

            Directory.Delete(directory, recursive: true);
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<ReportTemplateMetadata> SaveInternalAsync(
        ReportTemplateUploadRequest request,
        Stream content,
        bool overwriteTemplateId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);

        var extension = ReportTemplateContentTypeMapper.Normalize(Path.GetExtension(request.OriginalFileName));
        var resolvedType = ResolveTemplateType(request, extension, fallbackKind: request.Kind, fallbackTypeId: null);
        var templateId = NormalizeTemplateId(request.TemplateId) ?? CreateTemplateId(request.DisplayName);
        var safeFileName = $"template{extension}";
        var templateDirectory = Path.Combine(_storagePath, templateId);
        var templateFilePath = Path.Combine(templateDirectory, safeFileName);
        var metadataFilePath = Path.Combine(templateDirectory, MetadataFileName);

        var metadata = new ReportTemplateMetadata
        {
            Id = templateId,
            TemplateTypeId = resolvedType.TypeId,
            DisplayName = NormalizeRequired(request.DisplayName, nameof(request.DisplayName)),
            Kind = resolvedType.Kind,
            Description = NormalizeNullable(request.Description),
            SubjectTemplate = NormalizeNullable(request.SubjectTemplate),
            Scope = ReportTemplateScopeEvaluator.Normalize(request.Scope),
            FileName = safeFileName,
            Extension = extension,
            ContentType = resolvedType.ContentType,
            UploadedAt = DateTimeOffset.UtcNow,
            UploadedBy = NormalizeUser(request.UploadedBy),
            UpdatedAt = null,
            UpdatedBy = null,
            IsStarterTemplate = request.IsStarterTemplate
        };

        await _writeLock.WaitAsync(ct);
        try
        {
            if (Directory.Exists(templateDirectory) && !overwriteTemplateId)
                throw new InvalidOperationException($"A report template with ID '{templateId}' already exists.");

            Directory.CreateDirectory(templateDirectory);

            foreach (var path in Directory.GetFiles(templateDirectory))
            {
                File.Delete(path);
            }

            if (content.CanSeek)
                content.Position = 0;

            await using (var fileStream = File.Create(templateFilePath))
            {
                await content.CopyToAsync(fileStream, ct);
            }

            await WriteMetadataAsync(metadataFilePath, metadata, ct);
        }
        finally
        {
            _writeLock.Release();
        }

        return metadata;
    }

    private ReportTemplateTypeDefinition ResolveTemplateType(
        ReportTemplateUploadRequest request,
        string extension,
        ReportTemplateKind fallbackKind,
        string? fallbackTypeId)
    {
        var requestedTypeId = NormalizeNullable(request.TemplateTypeId) ?? fallbackTypeId;
        var resolved = _typeRegistry.Resolve(requestedTypeId, extension, fallbackKind);
        if (resolved is null)
        {
            var supported = string.Join(", ", _typeRegistry.GetAll().Select(type => $"{type.DisplayName} ({type.PrimaryExtension})"));
            throw new InvalidOperationException(
                $"Unsupported template type '{extension}'. Supported types: {supported}");
        }

        return resolved;
    }

    private ReportTemplateMetadata? ReadMetadata(string directory)
    {
        var metadataPath = Path.Combine(directory, MetadataFileName);
        if (!File.Exists(metadataPath))
            return null;

        try
        {
            var json = File.ReadAllText(metadataPath);
            var metadata = JsonSerializer.Deserialize<ReportTemplateMetadata>(json);
            return metadata is null ? null : NormalizeMetadata(metadata);
        }
        catch
        {
            return null;
        }
    }

    private ReportTemplateMetadata NormalizeMetadata(ReportTemplateMetadata metadata)
    {
        var resolvedType = _typeRegistry.Resolve(metadata.TemplateTypeId, metadata.Extension, metadata.Kind);

        return new ReportTemplateMetadata
        {
            Id = metadata.Id,
            TemplateTypeId = resolvedType?.TypeId ?? metadata.TemplateTypeId,
            DisplayName = metadata.DisplayName,
            Kind = resolvedType?.Kind ?? metadata.Kind,
            Description = metadata.Description,
            SubjectTemplate = metadata.SubjectTemplate,
            Scope = ReportTemplateScopeEvaluator.Normalize(metadata.Scope),
            FileName = metadata.FileName,
            Extension = ReportTemplateContentTypeMapper.Normalize(metadata.Extension),
            ContentType = resolvedType?.ContentType ?? metadata.ContentType,
            UploadedAt = metadata.UploadedAt,
            UploadedBy = string.IsNullOrWhiteSpace(metadata.UploadedBy) ? "unknown" : metadata.UploadedBy,
            UpdatedAt = metadata.UpdatedAt,
            UpdatedBy = metadata.UpdatedBy,
            IsStarterTemplate = metadata.IsStarterTemplate
        };
    }

    private static async Task WriteMetadataAsync(string path, ReportTemplateMetadata metadata, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(path, json, ct);
    }

    private static string NormalizeUser(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static string NormalizeRequired(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{parameterName} is required.");

        return value.Trim();
    }

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeTemplateId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var safe = new string(value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray())
            .Trim('-');

        return string.IsNullOrWhiteSpace(safe) ? null : safe;
    }

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
}
