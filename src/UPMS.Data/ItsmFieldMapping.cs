namespace UPMS.Data;

/// <summary>
/// Represents a single field name mapping from an ITSM source to a canonical name.
/// </summary>
public class ItsmFieldMapping
{
    public required string ItsmSource { get; init; }
    public required string SourceFieldName { get; init; }
    public required string CanonicalFieldName { get; init; }
}
