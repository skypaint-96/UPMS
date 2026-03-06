namespace UPMS.Web.Tests;

using Microsoft.Playwright;

/// <summary>
/// Base class for Playwright-based page tests. Manages a browser context and
/// page per test, disposing after each test completes.
/// </summary>
[TestFixture]
public abstract class PageTestBase
{
    protected IPage Page { get; private set; } = null!;
    private IBrowserContext? _context;

    /// <summary>
    /// Resolves a path relative to the running web application base URL.
    /// </summary>
    protected static string Url(string relativePath = "/")
    {
        return $"{WebTestFixture.BaseUrl}{relativePath}";
    }

    [SetUp]
    public async Task SetUpPage()
    {
        (_context, Page) = await WebTestFixture.NewPageAsync();
    }

    [TearDown]
    public async Task TearDownPage()
    {
        if (_context is not null) await _context.CloseAsync();
    }
}
