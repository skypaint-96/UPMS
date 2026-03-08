namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;

public class ItsmSourceService : IItsmSourceService
{
    private readonly UpmsDbContext _context;

    public ItsmSourceService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ItsmSource>> GetAllSourcesAsync()
    {
        return await _context.ItsmSources
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<ItsmSource?> GetSourceByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return await _context.ItsmSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name);
    }

    public async Task<ItsmSourceDefinition> GetSourceDefinitionAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var source = await _context.ItsmSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name)
            ?? throw new KeyNotFoundException($"ITSM source '{name}' was not found.");

        var mappings = await _context.ItsmFieldMappings
            .AsNoTracking()
            .Where(m => m.ItsmSource == name)
            .OrderBy(m => m.SourceFieldName)
            .ToListAsync();

        return new ItsmSourceDefinition
        {
            Source = source,
            Mappings = mappings.AsReadOnly()
        };
    }

    public async Task<ItsmSource> CreateSourceAsync(string name, string displayLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayLabel);

        var source = new ItsmSource
        {
            Name = name,
            DisplayLabel = displayLabel
        };

        _context.ItsmSources.Add(source);
        await _context.SaveChangesAsync();
        return source;
    }

    public async Task DeleteSourceAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string normalizedName = name.Trim();

        await using var tx = await _context.Database.BeginTransactionAsync();

        await _context.Snapshots
            .Where(s => s.ItsmSource == normalizedName)
            .ExecuteDeleteAsync();

        await _context.ItsmFieldMappings
            .Where(m => m.ItsmSource == normalizedName)
            .ExecuteDeleteAsync();

        int deletedSources = await _context.ItsmSources
            .Where(s => s.Name == normalizedName)
            .ExecuteDeleteAsync();

        if (deletedSources == 0)
            throw new KeyNotFoundException($"ITSM source '{normalizedName}' was not found.");

        await tx.CommitAsync();
    }

    public async Task UpsertMappingAsync(string sourceName, string sourceFieldName, string canonicalName, bool isRequired)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalName);

        string normalizedCanonicalName = canonicalName.Trim();
        string normalizedLookup = normalizedCanonicalName.ToLowerInvariant();

        var definition = await _context.CanonicalFieldDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Name.ToLower() == normalizedLookup)
            ?? throw new InvalidOperationException($"Canonical field '{normalizedCanonicalName}' is not registered. Add it on the Canonical Fields page first.");

        normalizedCanonicalName = definition.Name;
        bool effectiveRequired = isRequired || definition.IsSystemRequired;

        var existing = await _context.ItsmFieldMappings
            .FirstOrDefaultAsync(m => m.ItsmSource == sourceName && m.SourceFieldName == sourceFieldName);

        if (existing is null)
        {
            _context.ItsmFieldMappings.Add(new ItsmFieldMapping
            {
                ItsmSource = sourceName,
                SourceFieldName = sourceFieldName,
                CanonicalFieldName = normalizedCanonicalName,
                IsRequired = effectiveRequired
            });
        }
        else
        {
            existing.CanonicalFieldName = normalizedCanonicalName;
            existing.IsRequired = effectiveRequired;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteMappingAsync(string sourceName, string sourceFieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);

        await _context.ItsmFieldMappings
            .Where(m => m.ItsmSource == sourceName && m.SourceFieldName == sourceFieldName)
            .ExecuteDeleteAsync();
    }

    public async Task<IReadOnlyList<string>> GetRequiredFieldsAsync(string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var mappings = await _context.ItsmFieldMappings
            .AsNoTracking()
            .Where(m => m.ItsmSource == sourceName)
            .ToListAsync();

        var requiredCanonicalNames = (await _context.CanonicalFieldDefinitions
            .AsNoTracking()
            .Where(d => d.IsSystemRequired)
            .Select(d => d.Name)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var required = mappings
            .Where(m => m.IsRequired || requiredCanonicalNames.Contains(m.CanonicalFieldName))
            .OrderBy(m => m.SourceFieldName)
            .Select(m => m.SourceFieldName)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return required;
    }

    public async Task<string?> GetCanonicalNameAsync(string sourceName, string sourceFieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);

        return await _context.ItsmFieldMappings
            .AsNoTracking()
            .Where(m => m.ItsmSource == sourceName && m.SourceFieldName == sourceFieldName)
            .Select(m => m.CanonicalFieldName)
            .FirstOrDefaultAsync();
    }
}
