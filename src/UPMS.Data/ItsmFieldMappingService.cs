namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF Core–backed implementation of <see cref="IItsmFieldMappingService"/>.
/// </summary>
public class ItsmFieldMappingService : IItsmFieldMappingService
{
    private readonly UpmsDbContext _context;

    public ItsmFieldMappingService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public string GetCanonicalName(string itsmSource, string sourceFieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);

        var result = _context.ItsmFieldMappings
            .AsNoTracking()
            .Where(m => m.ItsmSource == itsmSource && m.SourceFieldName == sourceFieldName)
            .Select(m => m.CanonicalFieldName)
            .FirstOrDefault();

        return result ?? sourceFieldName;
    }

    /// <inheritdoc/>
    public IEnumerable<ItsmFieldMapping> GetMappingsForSource(string itsmSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSource);

        return _context.ItsmFieldMappings
            .AsNoTracking()
            .Where(m => m.ItsmSource == itsmSource)
            .OrderBy(m => m.SourceFieldName)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task UpsertMappingAsync(string itsmSource, string sourceFieldName, string canonicalFieldName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itsmSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFieldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalFieldName);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO itsm_field_mapping (itsm_source, source_field_name, canonical_field_name)
            VALUES ({itsmSource}, {sourceFieldName}, {canonicalFieldName})
            ON CONFLICT (itsm_source, source_field_name) DO UPDATE
                SET canonical_field_name = EXCLUDED.canonical_field_name
            """);
    }
}
