namespace UPMS.Web.Services;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UPMS.Data;

public class UpmsArchiveService : IUpmsArchiveService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly UpmsDbContext _dbContext;
    private readonly ICommandRepository _commandRepository;
    private readonly IItsmSourceService _itsmSourceService;
    private readonly ICanonicalFieldService _canonicalFieldService;
    private readonly ILogger<UpmsArchiveService> _logger;

    public UpmsArchiveService(
        UpmsDbContext dbContext,
        ICommandRepository commandRepository,
        IItsmSourceService itsmSourceService,
        ICanonicalFieldService canonicalFieldService,
        ILogger<UpmsArchiveService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _commandRepository = commandRepository ?? throw new ArgumentNullException(nameof(commandRepository));
        _itsmSourceService = itsmSourceService ?? throw new ArgumentNullException(nameof(itsmSourceService));
        _canonicalFieldService = canonicalFieldService ?? throw new ArgumentNullException(nameof(canonicalFieldService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ArchiveDownload> ExportSnapshotAsync(Guid snapshotId, CancellationToken ct = default)
    {
        var snapshot = await _dbContext.Snapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == snapshotId, ct)
            ?? throw new KeyNotFoundException($"Snapshot '{snapshotId}' was not found.");

        var snapshotTickets = await _dbContext.SnapshotTickets
            .AsNoTracking()
            .Where(st => st.SnapshotId == snapshotId)
            .OrderBy(st => st.CompanyName)
            .ThenBy(st => st.TicketKey)
            .ToListAsync(ct);

        var fieldChanges = await _dbContext.FieldChanges
            .AsNoTracking()
            .Where(fc => fc.SnapshotId == snapshotId)
            .OrderBy(fc => fc.CompanyName)
            .ThenBy(fc => fc.TicketKey)
            .ThenBy(fc => fc.ObservedAt)
            .ThenBy(fc => fc.Id)
            .ToListAsync(ct);

        var source = await _dbContext.ItsmSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == snapshot.ItsmSource, ct);

        var mappings = await _dbContext.ItsmFieldMappings
            .AsNoTracking()
            .Where(m => m.ItsmSource == snapshot.ItsmSource)
            .OrderBy(m => m.SourceFieldName)
            .ToListAsync(ct);

        var canonicalNames = mappings
            .Select(m => m.CanonicalFieldName)
            .Concat(fieldChanges
                .Where(fc => !string.IsNullOrWhiteSpace(fc.CanonicalFieldName))
                .Select(fc => fc.CanonicalFieldName!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var canonicalFields = (await _dbContext.CanonicalFieldDefinitions
            .AsNoTracking()
            .ToListAsync(ct))
            .Where(d => canonicalNames.Contains(d.Name))
            .OrderBy(d => d.Name)
            .ToList();

        var package = new SnapshotArchivePackage
        {
            ExportedAtUtc = DateTime.UtcNow,
            Source = source is null ? null : SourceArchiveModel.From(source),
            CanonicalFields = canonicalFields.Select(CanonicalFieldArchiveModel.From).ToList(),
            Mappings = mappings.Select(MappingArchiveModel.From).ToList(),
            Snapshot = SnapshotArchiveModel.From(snapshot),
            SnapshotTickets = snapshotTickets.Select(SnapshotTicketArchiveModel.From).ToList(),
            FieldChanges = fieldChanges.Select(FieldChangeArchiveModel.From).ToList()
        };

        byte[] content = JsonSerializer.SerializeToUtf8Bytes(package, SerializerOptions);
        string fileName = $"{SanitizeFileComponent(snapshot.ItsmSource)}-{snapshot.SnapshotDate:yyyyMMddHHmmss}-snapshot.json";
        return new ArchiveDownload(content, "application/json", fileName);
    }

    public async Task<ArchiveDownload> ExportSourceAsync(string sourceName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var source = await _dbContext.ItsmSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == sourceName, ct)
            ?? throw new KeyNotFoundException($"ITSM source '{sourceName}' was not found.");

        var mappings = await _dbContext.ItsmFieldMappings
            .AsNoTracking()
            .Where(m => m.ItsmSource == sourceName)
            .OrderBy(m => m.SourceFieldName)
            .ToListAsync(ct);

        var snapshots = await _dbContext.Snapshots
            .AsNoTracking()
            .Where(s => s.ItsmSource == sourceName)
            .OrderBy(s => s.SnapshotDate)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        var snapshotIds = snapshots.Select(s => s.Id).ToHashSet();

        var snapshotTickets = await _dbContext.SnapshotTickets
            .AsNoTracking()
            .Where(st => snapshotIds.Contains(st.SnapshotId))
            .OrderBy(st => st.SnapshotId)
            .ThenBy(st => st.CompanyName)
            .ThenBy(st => st.TicketKey)
            .ToListAsync(ct);

        var fieldChanges = await _dbContext.FieldChanges
            .AsNoTracking()
            .Where(fc => snapshotIds.Contains(fc.SnapshotId))
            .OrderBy(fc => fc.SnapshotId)
            .ThenBy(fc => fc.CompanyName)
            .ThenBy(fc => fc.TicketKey)
            .ThenBy(fc => fc.ObservedAt)
            .ThenBy(fc => fc.Id)
            .ToListAsync(ct);

        var canonicalNames = mappings
            .Select(m => m.CanonicalFieldName)
            .Concat(fieldChanges
                .Where(fc => !string.IsNullOrWhiteSpace(fc.CanonicalFieldName))
                .Select(fc => fc.CanonicalFieldName!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var canonicalFields = (await _dbContext.CanonicalFieldDefinitions
            .AsNoTracking()
            .ToListAsync(ct))
            .Where(d => canonicalNames.Contains(d.Name))
            .OrderBy(d => d.Name)
            .ToList();

        var snapshotBundles = snapshots
            .Select(snapshot => new SnapshotBundleArchiveModel
            {
                Snapshot = SnapshotArchiveModel.From(snapshot),
                SnapshotTickets = snapshotTickets
                    .Where(st => st.SnapshotId == snapshot.Id)
                    .Select(SnapshotTicketArchiveModel.From)
                    .ToList(),
                FieldChanges = fieldChanges
                    .Where(fc => fc.SnapshotId == snapshot.Id)
                    .Select(FieldChangeArchiveModel.From)
                    .ToList()
            })
            .ToList();

        var package = new ItsmSourceArchivePackage
        {
            ExportedAtUtc = DateTime.UtcNow,
            Source = SourceArchiveModel.From(source),
            CanonicalFields = canonicalFields.Select(CanonicalFieldArchiveModel.From).ToList(),
            Mappings = mappings.Select(MappingArchiveModel.From).ToList(),
            Snapshots = snapshotBundles
        };

        byte[] content = JsonSerializer.SerializeToUtf8Bytes(package, SerializerOptions);
        string fileName = $"{SanitizeFileComponent(sourceName)}-archive.json";
        return new ArchiveDownload(content, "application/json", fileName);
    }

    public async Task DeleteSnapshotAsync(Guid snapshotId, CancellationToken ct = default)
    {
        if (snapshotId == Guid.Empty)
            throw new ArgumentException("Snapshot ID cannot be empty.", nameof(snapshotId));

        int deleted = await _dbContext.Snapshots
            .Where(s => s.Id == snapshotId)
            .ExecuteDeleteAsync(ct);

        if (deleted == 0)
            throw new KeyNotFoundException($"Snapshot '{snapshotId}' was not found.");

        _logger.LogInformation("Snapshot {SnapshotId} and all related snapshot-ticket/field-change data were deleted.", snapshotId);
    }

    public async Task DeleteSourceAsync(string sourceName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        string normalizedSourceName = sourceName.Trim();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        int deletedSnapshots = await _dbContext.Snapshots
            .Where(s => s.ItsmSource == normalizedSourceName)
            .ExecuteDeleteAsync(ct);

        int deletedMappings = await _dbContext.ItsmFieldMappings
            .Where(m => m.ItsmSource == normalizedSourceName)
            .ExecuteDeleteAsync(ct);

        int deletedSources = await _dbContext.ItsmSources
            .Where(s => s.Name == normalizedSourceName)
            .ExecuteDeleteAsync(ct);

        if (deletedSources == 0)
            throw new KeyNotFoundException($"ITSM source '{normalizedSourceName}' was not found.");

        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "ITSM source {SourceName} was hard-deleted with {Snapshots} snapshots and {Mappings} mappings.",
            normalizedSourceName,
            deletedSnapshots,
            deletedMappings);
    }

    public async Task<SourceImportResult> ImportSourceAsync(Stream archiveStream, bool replaceExisting, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(archiveStream);

        var package = await JsonSerializer.DeserializeAsync<ItsmSourceArchivePackage>(archiveStream, SerializerOptions, ct)
            ?? throw new InvalidOperationException("The archive file could not be read.");

        if (!string.Equals(package.PackageType, "itsm-source", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This archive is not an ITSM source package.");

        if (package.Source is null || string.IsNullOrWhiteSpace(package.Source.Name))
            throw new InvalidOperationException("The archive does not contain a valid ITSM source definition.");

        string sourceName = package.Source.Name.Trim();
        var existing = await _dbContext.ItsmSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == sourceName, ct);

        if (existing is not null && !replaceExisting)
        {
            throw new InvalidOperationException($"ITSM source '{sourceName}' already exists. Enable replace to overwrite it.");
        }

        var canonicalDefinitions = BuildCanonicalDefinitions(package);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        if (existing is not null)
        {
            await _dbContext.Snapshots
                .Where(s => s.ItsmSource == sourceName)
                .ExecuteDeleteAsync(ct);

            await _dbContext.ItsmFieldMappings
                .Where(m => m.ItsmSource == sourceName)
                .ExecuteDeleteAsync(ct);

            await _dbContext.ItsmSources
                .Where(s => s.Name == sourceName)
                .ExecuteDeleteAsync(ct);
        }

        foreach (var definition in canonicalDefinitions.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            await _canonicalFieldService.UpsertAsync(definition.Name, definition.DataType, ct);
        }

        await _itsmSourceService.CreateSourceAsync(
            sourceName,
            string.IsNullOrWhiteSpace(package.Source.DisplayLabel) ? sourceName : package.Source.DisplayLabel.Trim());

        int importedMappings = 0;
        foreach (var mapping in package.Mappings
                     .Where(m => !string.IsNullOrWhiteSpace(m.SourceFieldName) && !string.IsNullOrWhiteSpace(m.CanonicalFieldName))
                     .OrderBy(m => m.SourceFieldName, StringComparer.OrdinalIgnoreCase))
        {
            await _itsmSourceService.UpsertMappingAsync(
                sourceName,
                mapping.SourceFieldName.Trim(),
                mapping.CanonicalFieldName.Trim(),
                mapping.IsRequired);

            importedMappings++;
        }

        int importedSnapshots = 0;
        int importedFieldChanges = 0;
        var usedSnapshotIds = new HashSet<Guid>();

        foreach (var snapshotBundle in package.Snapshots
                     .Where(s => s.Snapshot is not null)
                     .OrderBy(s => s.Snapshot.SnapshotDate)
                     .ThenBy(s => s.Snapshot.Id))
        {
            Guid requestedSnapshotId = snapshotBundle.Snapshot.Id;
            Guid effectiveSnapshotId = requestedSnapshotId;

            if (effectiveSnapshotId == Guid.Empty
                || usedSnapshotIds.Contains(effectiveSnapshotId)
                || await _dbContext.Snapshots.AsNoTracking().AnyAsync(s => s.Id == effectiveSnapshotId, ct))
            {
                effectiveSnapshotId = Guid.NewGuid();
            }

            usedSnapshotIds.Add(effectiveSnapshotId);

            await _commandRepository.CreateSnapshotAsync(
                new Snapshot
                {
                    Id = effectiveSnapshotId,
                    ItsmSource = sourceName,
                    SnapshotDate = snapshotBundle.Snapshot.SnapshotDate,
                    UploadedBy = NormalizeString(snapshotBundle.Snapshot.UploadedBy, "import"),
                    UploadedAt = snapshotBundle.Snapshot.UploadedAt == default ? DateTime.UtcNow : snapshotBundle.Snapshot.UploadedAt,
                    UploadMetadata = NormalizeNullableString(snapshotBundle.Snapshot.UploadMetadata)
                },
                ct);

            var snapshotTickets = snapshotBundle.SnapshotTickets
                .Where(st => !string.IsNullOrWhiteSpace(st.CompanyName) && !string.IsNullOrWhiteSpace(st.TicketKey))
                .Select(st => new SnapshotTicket
                {
                    Id = Guid.NewGuid(),
                    SnapshotId = effectiveSnapshotId,
                    CompanyName = st.CompanyName.Trim(),
                    TicketKey = st.TicketKey.Trim()
                })
                .ToList();

            if (snapshotTickets.Count > 0)
                await _commandRepository.AddSnapshotTicketsAsync(snapshotTickets, ct);

            var fieldChanges = snapshotBundle.FieldChanges
                .Where(fc => !string.IsNullOrWhiteSpace(fc.CompanyName)
                          && !string.IsNullOrWhiteSpace(fc.TicketKey)
                          && !string.IsNullOrWhiteSpace(fc.FieldName))
                .Select(fc => new FieldChange
                {
                    CompanyName = fc.CompanyName.Trim(),
                    TicketKey = fc.TicketKey.Trim(),
                    FieldName = fc.FieldName.Trim(),
                    CanonicalFieldName = NormalizeNullableString(fc.CanonicalFieldName),
                    FieldValue = NormalizeNullableString(fc.FieldValue),
                    ObservedAt = fc.ObservedAt == default ? snapshotBundle.Snapshot.SnapshotDate : fc.ObservedAt,
                    SnapshotId = effectiveSnapshotId
                })
                .ToList();

            if (fieldChanges.Count > 0)
                await _commandRepository.RecordFieldChangesAsync(fieldChanges, ct);

            importedSnapshots++;
            importedFieldChanges += fieldChanges.Count;
        }

        await transaction.CommitAsync(ct);

        _logger.LogInformation(
            "Imported ITSM source archive for {SourceName}: {Mappings} mappings, {Snapshots} snapshots, {FieldChanges} field changes.",
            sourceName,
            importedMappings,
            importedSnapshots,
            importedFieldChanges);

        return new SourceImportResult(sourceName, importedMappings, importedSnapshots, importedFieldChanges);
    }

    private static Dictionary<string, CanonicalFieldImportDefinition> BuildCanonicalDefinitions(ItsmSourceArchivePackage package)
    {
        var lookup = new Dictionary<string, CanonicalFieldImportDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in package.CanonicalFields)
        {
            if (string.IsNullOrWhiteSpace(field.Name))
                continue;

            lookup[field.Name.Trim()] = new CanonicalFieldImportDefinition(
                field.Name.Trim(),
                field.DataType,
                field.IsSystemRequired);
        }

        foreach (var mapping in package.Mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.CanonicalFieldName))
                continue;

            string canonicalName = mapping.CanonicalFieldName.Trim();
            if (!lookup.ContainsKey(canonicalName))
            {
                lookup[canonicalName] = new CanonicalFieldImportDefinition(
                    canonicalName,
                    CanonicalFieldDataType.Text,
                    CanonicalFieldDefaults.IsSystemRequiredName(canonicalName));
            }
        }

        foreach (var snapshot in package.Snapshots)
        {
            foreach (var fieldChange in snapshot.FieldChanges)
            {
                if (string.IsNullOrWhiteSpace(fieldChange.CanonicalFieldName))
                    continue;

                string canonicalName = fieldChange.CanonicalFieldName.Trim();
                if (!lookup.ContainsKey(canonicalName))
                {
                    lookup[canonicalName] = new CanonicalFieldImportDefinition(
                        canonicalName,
                        CanonicalFieldDataType.Text,
                        CanonicalFieldDefaults.IsSystemRequiredName(canonicalName));
                }
            }
        }

        return lookup;
    }

    private static string NormalizeString(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? NormalizeNullableString(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SanitizeFileComponent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "upms";

        var invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
        var sanitized = new string(value.Trim().Select(c => invalidChars.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "upms" : sanitized;
    }

    private sealed record CanonicalFieldImportDefinition(
        string Name,
        CanonicalFieldDataType DataType,
        bool IsSystemRequired);

    private sealed class SnapshotArchivePackage
    {
        public string PackageType { get; init; } = "snapshot";
        public int PackageVersion { get; init; } = 1;
        public DateTime ExportedAtUtc { get; init; }
        public SourceArchiveModel? Source { get; init; }
        public List<CanonicalFieldArchiveModel> CanonicalFields { get; init; } = [];
        public List<MappingArchiveModel> Mappings { get; init; } = [];
        public SnapshotArchiveModel Snapshot { get; init; } = new();
        public List<SnapshotTicketArchiveModel> SnapshotTickets { get; init; } = [];
        public List<FieldChangeArchiveModel> FieldChanges { get; init; } = [];
    }

    private sealed class ItsmSourceArchivePackage
    {
        public string PackageType { get; init; } = "itsm-source";
        public int PackageVersion { get; init; } = 1;
        public DateTime ExportedAtUtc { get; init; }
        public SourceArchiveModel Source { get; init; } = new();
        public List<CanonicalFieldArchiveModel> CanonicalFields { get; init; } = [];
        public List<MappingArchiveModel> Mappings { get; init; } = [];
        public List<SnapshotBundleArchiveModel> Snapshots { get; init; } = [];
    }

    private sealed class SourceArchiveModel
    {
        public string Name { get; init; } = string.Empty;
        public string DisplayLabel { get; init; } = string.Empty;

        public static SourceArchiveModel From(ItsmSource source) => new()
        {
            Name = source.Name,
            DisplayLabel = source.DisplayLabel
        };
    }

    private sealed class CanonicalFieldArchiveModel
    {
        public string Name { get; init; } = string.Empty;
        public CanonicalFieldDataType DataType { get; init; }
        public bool IsSystemRequired { get; init; }

        public static CanonicalFieldArchiveModel From(CanonicalFieldDefinition definition) => new()
        {
            Name = definition.Name,
            DataType = definition.DataType,
            IsSystemRequired = definition.IsSystemRequired
        };
    }

    private sealed class MappingArchiveModel
    {
        public string SourceFieldName { get; init; } = string.Empty;
        public string CanonicalFieldName { get; init; } = string.Empty;
        public bool IsRequired { get; init; }

        public static MappingArchiveModel From(ItsmFieldMapping mapping) => new()
        {
            SourceFieldName = mapping.SourceFieldName,
            CanonicalFieldName = mapping.CanonicalFieldName,
            IsRequired = mapping.IsRequired
        };
    }

    private sealed class SnapshotBundleArchiveModel
    {
        public SnapshotArchiveModel Snapshot { get; init; } = new();
        public List<SnapshotTicketArchiveModel> SnapshotTickets { get; init; } = [];
        public List<FieldChangeArchiveModel> FieldChanges { get; init; } = [];
    }

    private sealed class SnapshotArchiveModel
    {
        public Guid Id { get; init; }
        public DateTime SnapshotDate { get; init; }
        public string UploadedBy { get; init; } = string.Empty;
        public DateTime UploadedAt { get; init; }
        public string? UploadMetadata { get; init; }

        public static SnapshotArchiveModel From(Snapshot snapshot) => new()
        {
            Id = snapshot.Id,
            SnapshotDate = snapshot.SnapshotDate,
            UploadedBy = snapshot.UploadedBy,
            UploadedAt = snapshot.UploadedAt,
            UploadMetadata = snapshot.UploadMetadata
        };
    }

    private sealed class SnapshotTicketArchiveModel
    {
        public Guid Id { get; init; }
        public string CompanyName { get; init; } = string.Empty;
        public string TicketKey { get; init; } = string.Empty;

        public static SnapshotTicketArchiveModel From(SnapshotTicket snapshotTicket) => new()
        {
            Id = snapshotTicket.Id,
            CompanyName = snapshotTicket.CompanyName,
            TicketKey = snapshotTicket.TicketKey
        };
    }

    private sealed class FieldChangeArchiveModel
    {
        public string CompanyName { get; init; } = string.Empty;
        public string TicketKey { get; init; } = string.Empty;
        public string FieldName { get; init; } = string.Empty;
        public string? CanonicalFieldName { get; init; }
        public string? FieldValue { get; init; }
        public DateTime ObservedAt { get; init; }

        public static FieldChangeArchiveModel From(FieldChange fieldChange) => new()
        {
            CompanyName = fieldChange.CompanyName,
            TicketKey = fieldChange.TicketKey,
            FieldName = fieldChange.FieldName,
            CanonicalFieldName = fieldChange.CanonicalFieldName,
            FieldValue = fieldChange.FieldValue,
            ObservedAt = fieldChange.ObservedAt
        };
    }
}
