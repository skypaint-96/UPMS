namespace UPMS.Data;

/// <summary>
/// Manages ITSM source definitions and their field mappings.
/// </summary>
public interface IItsmSourceService
{
    /// <summary>Returns all defined ITSM sources.</summary>
    Task<IReadOnlyList<ItsmSource>> GetAllSourcesAsync();

    /// <summary>Returns the source with the given <paramref name="name"/>, or <c>null</c> if not found.</summary>
    Task<ItsmSource?> GetSourceByNameAsync(string name);

    /// <summary>
    /// Returns the full source definition (source + all mappings) for <paramref name="name"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when no source with that name exists.</exception>
    Task<ItsmSourceDefinition> GetSourceDefinitionAsync(string name);

    /// <summary>Creates a new ITSM source with the given slug <paramref name="name"/> and <paramref name="displayLabel"/>.</summary>
    Task<ItsmSource> CreateSourceAsync(string name, string displayLabel);

    /// <summary>Deletes the source identified by <paramref name="name"/> and all its field mappings.</summary>
    Task DeleteSourceAsync(string name);

    /// <summary>
    /// Adds or updates the mapping for <paramref name="sourceFieldName"/> within the source identified by
    /// <paramref name="sourceName"/>.
    /// </summary>
    Task UpsertMappingAsync(string sourceName, string sourceFieldName, string canonicalName, bool isRequired);

    /// <summary>Removes the mapping for <paramref name="sourceFieldName"/> from the given source.</summary>
    Task DeleteMappingAsync(string sourceName, string sourceFieldName);

    /// <summary>
    /// Returns the <c>source_field_name</c> values where <c>is_required = true</c> for the given source.
    /// </summary>
    Task<IReadOnlyList<string>> GetRequiredFieldsAsync(string sourceName);

    /// <summary>
    /// Returns the canonical name for <paramref name="sourceFieldName"/> within <paramref name="sourceName"/>,
    /// or <c>null</c> if no mapping exists.
    /// </summary>
    Task<string?> GetCanonicalNameAsync(string sourceName, string sourceFieldName);
}
