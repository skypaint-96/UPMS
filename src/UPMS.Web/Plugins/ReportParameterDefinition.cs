namespace UPMS.Web.Plugins;

/// <summary>
/// Describes a single input parameter that a report plugin requires.
/// </summary>
public class ReportParameterDefinition
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required ReportParameterType Type { get; init; }
    public bool IsRequired { get; init; } = true;
    public string? Description { get; init; }
    public IReadOnlyList<string>? Options { get; init; }  // For Select/MultiSelect types
}
