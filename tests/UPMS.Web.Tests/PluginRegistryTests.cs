namespace UPMS.Web.Tests;

using UPMS.Web.Plugins;

/// <summary>
/// Unit tests for the plugin registry infrastructure and the StubReportPlugin.
/// These tests do not require a web server or browser — they test types directly.
/// </summary>
[TestFixture]
public class PluginRegistryTests
{
    [Test]
    public void PluginRegistry_WithDuplicatePluginIds_Throws()
    {
        // Arrange
        var plugin1 = new StubReportPlugin();
        var plugin2 = new StubReportPlugin();

        // Act + Assert
        Assert.Throws<InvalidOperationException>(() => new PluginRegistry([plugin1, plugin2]));
    }

    [Test]
    public void PluginRegistry_WithNoPlugins_ReturnsEmptyList()
    {
        // Arrange
        var registry = new PluginRegistry([]);

        // Act
        var plugins = registry.GetAll();

        // Assert
        Assert.That(plugins, Is.Empty);
    }

    [Test]
    public void PluginRegistry_WithOnePlugin_GetAllReturnsIt()
    {
        // Arrange
        var plugin = new StubReportPlugin();
        var registry = new PluginRegistry([plugin]);

        // Act
        var plugins = registry.GetAll();

        // Assert
        Assert.That(plugins, Has.Count.EqualTo(1));
    }

    [Test]
    public void PluginRegistry_GetById_ReturnsCorrectPlugin()
    {
        // Arrange
        var plugin = new StubReportPlugin();
        var registry = new PluginRegistry([plugin]);

        // Act
        var result = registry.GetById("stub-plugin");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.PluginId, Is.EqualTo("stub-plugin"));
    }

    [Test]
    public void PluginRegistry_GetById_UnknownId_ReturnsNull()
    {
        // Arrange
        var registry = new PluginRegistry([new StubReportPlugin()]);

        // Act
        var result = registry.GetById("unknown");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void PluginRegistry_HasPlugin_ReturnsTrueForRegistered()
    {
        // Arrange
        var registry = new PluginRegistry([new StubReportPlugin()]);

        // Act
        var result = registry.HasPlugin("stub-plugin");

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void PluginRegistry_HasPlugin_ReturnsFalseForUnknown()
    {
        // Arrange
        var registry = new PluginRegistry([new StubReportPlugin()]);

        // Act
        var result = registry.HasPlugin("unknown");

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void StubPlugin_HasCorrectPluginId()
    {
        // Arrange
        var plugin = new StubReportPlugin();

        // Assert
        Assert.That(plugin.PluginId, Is.EqualTo("stub-plugin"));
    }

    [Test]
    public void StubPlugin_HasDisplayName()
    {
        // Arrange
        var plugin = new StubReportPlugin();

        // Assert
        Assert.That(plugin.DisplayName, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void StubPlugin_HasDescription()
    {
        // Arrange
        var plugin = new StubReportPlugin();

        // Assert
        Assert.That(plugin.Description, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void StubPlugin_Parameters_HasCompanyParam()
    {
        // Arrange
        var plugin = new StubReportPlugin();

        // Assert
        Assert.That(plugin.Parameters, Has.Some.Matches<ReportParameterDefinition>(p => p.Key == "company"));
    }

    [Test]
    public void StubPlugin_Parameters_HasFromDateParam()
    {
        // Arrange
        var plugin = new StubReportPlugin();

        // Assert
        Assert.That(plugin.Parameters, Has.Some.Matches<ReportParameterDefinition>(p => p.Key == "from_date"));
    }

    [Test]
    public async Task StubPlugin_GenerateAsync_WithCompanyParam_ReturnsSuccess()
    {
        // Arrange
        var plugin = new StubReportPlugin();
        var request = new ReportRequest
        {
            PluginId = "stub-plugin",
            Parameters = new Dictionary<string, string> { ["company"] = "Acme" }
        };

        // Act
        var result = await plugin.GenerateAsync(request);

        // Assert
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task StubPlugin_GenerateAsync_WithoutCompanyParam_ReturnsFailure()
    {
        // Arrange
        var plugin = new StubReportPlugin();
        var request = new ReportRequest
        {
            PluginId = "stub-plugin",
            Parameters = new Dictionary<string, string>()
        };

        // Act
        var result = await plugin.GenerateAsync(request);

        // Assert
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void ReportParameterDefinition_RequiredIsDefault_True()
    {
        // Arrange — only set the truly required fields
        var param = new ReportParameterDefinition
        {
            Key = "test",
            DisplayName = "Test",
            Type = ReportParameterType.Text
        };

        // Assert — IsRequired defaults to true
        Assert.That(param.IsRequired, Is.True);
    }
}
