namespace UPMS.Web.Tests;

/// <summary>
/// Placeholder tests for the email notification report plugin (Stage 7).
/// All tests are ignored until Stage 7 is implemented.
/// </summary>
[TestFixture]
public class EmailNotificationPluginTests
{
    [Test]
    public void EmailPlugin_ImplementsIReportPlugin()
    {
        Assert.Ignore("Stage 7 not yet implemented — EmailNotificationPlugin must implement IReportPlugin.");
    }

    [Test]
    public void EmailPlugin_HasPluginId()
    {
        Assert.Ignore("Stage 7 not yet implemented — EmailNotificationPlugin must expose a non-empty PluginId.");
    }

    [Test]
    public void EmailPlugin_OffersMultipleTemplateOptions()
    {
        Assert.Ignore("Stage 7 not yet implemented — EmailNotificationPlugin must expose a template Select parameter with at least two options.");
    }

    [Test]
    public void EmailPlugin_Parameters_ContainsTemplateParam()
    {
        Assert.Ignore("Stage 7 not yet implemented — EmailNotificationPlugin.Parameters must include a 'template' parameter of type Select.");
    }

    [Test]
    public void EmailPlugin_GenerateAsync_ReturnsHtmlContent()
    {
        Assert.Ignore("Stage 7 not yet implemented — EmailNotificationPlugin.GenerateAsync must return ReportOutputType.HtmlContent.");
    }

    [Test]
    public void EmailPlugin_GenerateAsync_PopulatesTemplateWithData()
    {
        Assert.Ignore("Stage 7 not yet implemented — the generated HTML must be populated with ticket/snapshot data from the supplied parameters.");
    }
}
