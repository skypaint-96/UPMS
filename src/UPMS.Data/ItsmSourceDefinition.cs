namespace UPMS.Data;

/// <summary>
/// Combines an <see cref="ItsmSource"/> with its full set of field mappings.
/// </summary>
public class ItsmSourceDefinition
{
    public required ItsmSource Source { get; init; }
    public required IReadOnlyList<ItsmFieldMapping> Mappings { get; init; }
}
