namespace UPMS.Data;

/// <summary>
/// Represents a named ITSM source instance (one row from the <c>itsm_source</c> table).
/// </summary>
public class ItsmSource
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string DisplayLabel { get; init; }
}
