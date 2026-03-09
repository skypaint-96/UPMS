namespace UPMS.Reporting.Plugins;

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

    /// <summary>
    /// Optional placeholder or example text shown for text-like inputs.
    /// </summary>
    public string? Placeholder { get; init; }

    /// <summary>
    /// Optional canonical field name backing this parameter. When supplied, the UI can
    /// use the registered data type and value suggestions for a richer input experience.
    /// </summary>
    public string? CanonicalFieldName { get; init; }

    /// <summary>
    /// Enables value suggestions when the parameter is backed by a canonical field.
    /// </summary>
    public bool EnableSuggestions { get; init; } = true;
}
