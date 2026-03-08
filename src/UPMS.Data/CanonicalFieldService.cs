namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;

public class CanonicalFieldService : ICanonicalFieldService
{
    private readonly UpmsDbContext _context;

    public CanonicalFieldService(UpmsDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<CanonicalFieldDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.CanonicalFieldDefinitions
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
    }

    public async Task<CanonicalFieldDefinition?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string normalizedName = name.Trim();
        string normalizedLookup = normalizedName.ToLowerInvariant();

        return await _context.CanonicalFieldDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Name.ToLower() == normalizedLookup, ct);
    }

    public async Task<IReadOnlyDictionary<string, CanonicalFieldDefinition>> GetLookupAsync(CancellationToken ct = default)
    {
        var rows = await _context.CanonicalFieldDefinitions
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
    }

    public async Task UpsertAsync(string name, CanonicalFieldDataType dataType, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string normalizedName = name.Trim();
        string normalizedLookup = normalizedName.ToLowerInvariant();
        bool isSystemRequired = CanonicalFieldDefaults.IsSystemRequiredName(normalizedName);

        var existing = await _context.CanonicalFieldDefinitions
            .FirstOrDefaultAsync(d => d.Name.ToLower() == normalizedLookup, ct);

        if (existing is null)
        {
            _context.CanonicalFieldDefinitions.Add(new CanonicalFieldDefinition
            {
                Name = normalizedName,
                DataType = dataType,
                IsSystemRequired = isSystemRequired
            });
        }
        else
        {
            existing.DataType = dataType;
            existing.IsSystemRequired = existing.IsSystemRequired || isSystemRequired;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string normalizedName = name.Trim();
        string normalizedLookup = normalizedName.ToLowerInvariant();

        var existing = await _context.CanonicalFieldDefinitions
            .FirstOrDefaultAsync(d => d.Name.ToLower() == normalizedLookup, ct)
            ?? throw new KeyNotFoundException($"Canonical field '{normalizedName}' was not found.");

        if (existing.IsSystemRequired || CanonicalFieldDefaults.IsSystemRequiredName(normalizedName))
        {
            throw new InvalidOperationException($"Canonical field '{normalizedName}' is required by the ticket key system and cannot be removed.");
        }

        bool isMapped = await _context.ItsmFieldMappings
            .AsNoTracking()
            .AnyAsync(m => m.CanonicalFieldName.ToLower() == normalizedLookup, ct);

        if (isMapped)
        {
            throw new InvalidOperationException($"Canonical field '{normalizedName}' is still referenced by one or more ITSM field mappings.");
        }

        bool hasHistoricalData = await _context.FieldChanges
            .AsNoTracking()
            .AnyAsync(fc => fc.CanonicalFieldName != null && fc.CanonicalFieldName.ToLower() == normalizedLookup, ct);

        if (hasHistoricalData)
        {
            throw new InvalidOperationException($"Canonical field '{normalizedName}' is still referenced by historical field-change data.");
        }

        _context.CanonicalFieldDefinitions.Remove(existing);
        await _context.SaveChangesAsync(ct);
    }
}
