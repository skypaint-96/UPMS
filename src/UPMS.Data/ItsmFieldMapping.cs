namespace UPMS.Data;

/// <summary>
/// Represents a single field name mapping from an ITSM source to a canonical name
/// (<c>itsm_field_mapping</c> table). Composite PK: (<c>itsm_source</c>, <c>source_field_name</c>).
/// </summary>
public class ItsmFieldMapping
{
    /// <summary>Parameterless constructor required by EF Core and Dapper.</summary>
    public ItsmFieldMapping() { }

    public string ItsmSource { get; set; } = string.Empty;
    public string SourceFieldName { get; set; } = string.Empty;
    public string CanonicalFieldName { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c>, this source field name must be present as a column header
    /// in every CSV uploaded for the owning ITSM source. Absence causes a validation error.
    /// </summary>
    public bool IsRequired { get; set; }
}
