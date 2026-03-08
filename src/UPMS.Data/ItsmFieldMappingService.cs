namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;

public class ItsmFieldMappingService : IItsmFieldMappingService
{
    private readonly UpmsDbContext _context;

    public ItsmFieldMappingService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public string GetCanonicalName(string itsmSource, string sourceFieldName)
    {
        ArgumentNullException.ThrowIfNull(itsmSource);
        ArgumentNullException.ThrowIfNull(sourceFieldName);

        var mapping = _context.ItsmFieldMappings
            .AsNoTracking()
            .FirstOrDefault(m => m.ItsmSource == itsmSource && m.SourceFieldName == sourceFieldName);

        return mapping?.CanonicalFieldName ?? sourceFieldName;
    }

    public IEnumerable<ItsmFieldMapping> GetMappingsForSource(string itsmSource)
    {
        ArgumentNullException.ThrowIfNull(itsmSource);

        return _context.ItsmFieldMappings
            .AsNoTracking()
            .Where(m => m.ItsmSource == itsmSource)
            .OrderBy(m => m.SourceFieldName)
            .ToList();
    }

    public async Task UpsertMappingAsync(string itsmSource, string sourceFieldName, string canonicalFieldName)
    {
        ArgumentNullException.ThrowIfNull(itsmSource);
        ArgumentNullException.ThrowIfNull(sourceFieldName);
        ArgumentNullException.ThrowIfNull(canonicalFieldName);

        string normalizedCanonicalName = canonicalFieldName.Trim();
        string normalizedLookup = normalizedCanonicalName.ToLowerInvariant();

        var definition = await _context.CanonicalFieldDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Name.ToLower() == normalizedLookup)
            ?? throw new InvalidOperationException($"Canonical field '{normalizedCanonicalName}' is not registered.");

        normalizedCanonicalName = definition.Name;

        var existing = await _context.ItsmFieldMappings
            .FirstOrDefaultAsync(m => m.ItsmSource == itsmSource && m.SourceFieldName == sourceFieldName);

        if (existing is null)
        {
            _context.ItsmFieldMappings.Add(new ItsmFieldMapping
            {
                ItsmSource = itsmSource,
                SourceFieldName = sourceFieldName,
                CanonicalFieldName = normalizedCanonicalName,
                IsRequired = definition.IsSystemRequired
            });
        }
        else
        {
            existing.CanonicalFieldName = normalizedCanonicalName;
            existing.IsRequired = existing.IsRequired || definition.IsSystemRequired;
        }

        await _context.SaveChangesAsync();
    }
}
