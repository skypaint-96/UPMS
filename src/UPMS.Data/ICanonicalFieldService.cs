namespace UPMS.Data;

public interface ICanonicalFieldService
{
    Task<IReadOnlyList<CanonicalFieldDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<CanonicalFieldDefinition?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, CanonicalFieldDefinition>> GetLookupAsync(CancellationToken ct = default);
    Task UpsertAsync(string name, CanonicalFieldDataType dataType, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
}
