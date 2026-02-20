namespace UPMS.Data;

/// <summary>
/// Represents a single field name mapping from an ITSM source to a canonical name.
/// </summary>
public class ItsmFieldMapping
{
    public required string ItsmSource { get; init; }
    public required string SourceFieldName { get; init; }
    public required string CanonicalFieldName { get; init; }

    /// <summary>
    /// When <c>true</c>, this source field name must be present as a column header
    /// in every CSV uploaded for the owning ITSM source. Absence causes a validation error.
    /// </summary>
    public bool IsRequired { get; init; }
}
