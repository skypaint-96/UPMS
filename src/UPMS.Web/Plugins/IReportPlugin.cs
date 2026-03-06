namespace UPMS.Web.Plugins;

/// <summary>
/// Contract that all reporting plugins must implement.
/// </summary>
public interface IReportPlugin
{
    string PluginId { get; }
    string DisplayName { get; }
    string Description { get; }
    IReadOnlyList<ReportParameterDefinition> Parameters { get; }
    Task<ReportResult> GenerateAsync(ReportRequest request, CancellationToken ct = default);
}
