namespace UPMS.Ingestion;

using UPMS.Data;

public static class CanonicalFieldCatalog
{
    public static readonly IReadOnlyList<string> Names = CanonicalFieldDefaults.All
        .Select(d => d.Name)
        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
        .ToList()
        .AsReadOnly();

    public static IReadOnlyList<string> GetAliases(string fieldName)
        => CanonicalFieldAliasRegistry.GetAliases(fieldName);
}
