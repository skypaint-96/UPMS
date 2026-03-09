namespace UPMS.Reporting;

using UPMS.Reporting.Plugins;

public interface IReportExecutionService
{
    IReadOnlyList<IReportPlugin> GetPlugins();
    IReportPlugin? GetPlugin(string pluginId);
    Task<ReportResult> ExecuteAsync(
        string pluginId,
        IReadOnlyDictionary<string, string> parameters,
        string? requestedBy,
        CancellationToken ct = default);
}
