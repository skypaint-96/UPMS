namespace UPMS.Data;

/// <summary>
/// Provides canonical field name lookups for ITSM source field names.
/// </summary>
public interface IItsmFieldMappingService
{
    /// <summary>
    /// Returns the canonical field name for the given ITSM source and source field name.
    /// If no mapping is found, returns the source field name unchanged (graceful fallback).
    /// </summary>
    string GetCanonicalName(string itsmSource, string sourceFieldName);

    /// <summary>
    /// Returns all mappings for a given ITSM source.
    /// </summary>
    IEnumerable<ItsmFieldMapping> GetMappingsForSource(string itsmSource);

    /// <summary>
    /// Adds or updates a mapping.
    /// </summary>
    Task UpsertMappingAsync(string itsmSource, string sourceFieldName, string canonicalFieldName);
}
