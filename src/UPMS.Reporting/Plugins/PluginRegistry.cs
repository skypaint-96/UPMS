namespace UPMS.Reporting.Plugins;

/// <summary>
/// Discovers and provides access to all registered IReportPlugin implementations.
/// </summary>
public class PluginRegistry
{
    private readonly IReadOnlyList<IReportPlugin> _plugins;

    public PluginRegistry(IEnumerable<IReportPlugin> plugins)
    {
        var list = plugins.ToList();

        var duplicates = list
            .GroupBy(p => p.PluginId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (duplicates.Count > 0)
            throw new InvalidOperationException($"Duplicate report plugin IDs detected: {string.Join(", ", duplicates)}");

        _plugins = list.AsReadOnly();
    }

    public IReadOnlyList<IReportPlugin> GetAll() => _plugins;

    public IReportPlugin? GetById(string pluginId) =>
        _plugins.FirstOrDefault(p => p.PluginId == pluginId);

    public bool HasPlugin(string pluginId) => GetById(pluginId) is not null;
}
