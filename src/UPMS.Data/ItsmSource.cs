namespace UPMS.Data;

/// <summary>
/// Represents a named ITSM source instance (one row from the <c>itsm_source</c> table).
/// </summary>
public class ItsmSource
{
    /// <summary>Parameterless constructor required by EF Core and Dapper.</summary>
    public ItsmSource() { }

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayLabel { get; set; } = string.Empty;
}
