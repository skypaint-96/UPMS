namespace UPMS.Web.Plugins;

/// <summary>
/// Discovers and provides access to all registered IReportPlugin implementations.
/// </summary>
public class PluginRegistry
{
    private readonly IReadOnlyList<IReportPlugin> _plugins;

    public PluginRegistry(IEnumerable<IReportPlugin> plugins)
    {
        _plugins = plugins.ToList().AsReadOnly();
    }

    public IReadOnlyList<IReportPlugin> GetAll() => _plugins;

    public IReportPlugin? GetById(string pluginId) =>
        _plugins.FirstOrDefault(p => p.PluginId == pluginId);

    public bool HasPlugin(string pluginId) => GetById(pluginId) is not null;
}
