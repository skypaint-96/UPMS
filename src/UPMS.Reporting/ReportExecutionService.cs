namespace UPMS.Reporting;

using UPMS.Reporting.Plugins;

public class ReportExecutionService : IReportExecutionService
{
    private readonly PluginRegistry _registry;

    public ReportExecutionService(PluginRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public IReadOnlyList<IReportPlugin> GetPlugins() => _registry.GetAll();

    public IReportPlugin? GetPlugin(string pluginId) => _registry.GetById(pluginId);

    public async Task<ReportResult> ExecuteAsync(
        string pluginId,
        IReadOnlyDictionary<string, string> parameters,
        string? requestedBy,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(parameters);

        var plugin = _registry.GetById(pluginId);
        if (plugin is null)
            return ReportResult.Failure($"Report plugin '{pluginId}' is not registered.");

        return await plugin.GenerateAsync(
            new ReportRequest
            {
                PluginId = pluginId,
                Parameters = new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase),
                RequestedBy = requestedBy,
                RequestedAt = DateTimeOffset.UtcNow
            },
            ct);
    }
}
