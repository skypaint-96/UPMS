namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core–backed implementation of <see cref="IItsmSourceService"/>.
/// </summary>
public class ItsmSourceService : IItsmSourceService
{
    private readonly UpmsDbContext _context;

    public ItsmSourceService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ItsmSource>> GetAllSourcesAsync()
    {
        return await _context.ItsmSources
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<ItsmSource?> GetSourceByNameAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return await _context.ItsmSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name);
    }

    /// <inheritdoc/>
    public async Task<ItsmSourceDefinition> GetSourceDefinitionAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var source = await GetSourceByNameAsync(name)
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public async Task DeleteSourceAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        // Delete mappings first (no DB-level FK cascade between itsm_field_mapping and itsm_source)
        await _context.ItsmFieldMappings
            .Where(m => m.ItsmSource == name)
            .ExecuteDeleteAsync();

        await _context.ItsmSources
            .Where(s => s.Name == name)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc/>
    public async Task UpsertMappingAsync(string sourceName, string sourceFieldName, string canonicalName, bool isRequired)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalName);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name, is_required)
            VALUES ({sourceName}, {sourceFieldName}, {canonicalName}, {isRequired})
            ON CONFLICT (itsm_source, source_field_name) DO UPDATE
                SET canonical_field_name = EXCLUDED.canonical_field_name,
                    is_required          = EXCLUDED.is_required
            """);
    }

    /// <inheritdoc/>
    public async Task DeleteMappingAsync(string sourceName, string sourceFieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);

        await _context.ItsmFieldMappings
            .Where(m => m.ItsmSource == sourceName && m.SourceFieldName == sourceFieldName)
            .ExecuteDeleteAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetRequiredFieldsAsync(string sourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        return await _context.ItsmFieldMappings
            .AsNoTracking()
            .Where(m => m.ItsmSource == sourceName && m.IsRequired)
            .OrderBy(m => m.SourceFieldName)
            .Select(m => m.SourceFieldName)
            .ToListAsync();
    }

    /// <inheritdoc/>
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
