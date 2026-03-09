namespace UPMS.Reporting.Plugins;

/// <summary>
/// Input model passed to a plugin's GenerateAsync method.
/// </summary>
public class ReportRequest
{
    public required string PluginId { get; init; }
    public required IDictionary<string, string> Parameters { get; init; }
    public string? RequestedBy { get; init; }
    public DateTimeOffset RequestedAt { get; init; } = DateTimeOffset.UtcNow;
}
