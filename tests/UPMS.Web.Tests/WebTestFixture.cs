namespace UPMS.Web.Tests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using UPMS.Data;

/// <summary>
/// Starts the UPMS.Web application on a real Kestrel port and provides
/// Playwright browser/page instances for end-to-end tests.
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> so that static
/// assets, content root, and Razor component discovery work automatically.
/// </summary>
[SetUpFixture]
public class WebTestFixture
{
    private static WebApplicationFactory<Program>? _factory;
    private static IPlaywright? _playwright;
    private static IBrowser? _browser;

    // Resolve test connection string: env var takes priority, then local default.
    private static readonly string TestConnectionString =
        Environment.GetEnvironmentVariable("UPMS_TEST_CONNECTION_STRING")
        ?? "Host=localhost;Port=5433;Database=upms_test;Username=upms;Password=upms_test";

    /// <summary>
    /// Base URL of the running web application (e.g. "http://127.0.0.1:5001").
    /// </summary>
    public static string BaseUrl { get; private set; } = string.Empty;

    [OneTimeSetUp]
    public async Task GlobalSetUp()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseUrls("http://127.0.0.1:0");

                builder.ConfigureServices(services =>
                {
                    // Override the database connection string so the web app
                    // points at the test database rather than a production one.
                    services.Configure<DatabaseOptions>(options =>
                        options.ConnectionString = TestConnectionString);
                });

                // Ensure the UPMS_CONNECTION_STRING env var seen by Program.cs
                // also resolves to the test database.
                builder.UseSetting(
                    "UPMS_CONNECTION_STRING_OVERRIDE", TestConnectionString);
            });

        // Accessing Services forces the host to start; then retrieve bound address.
        IServer server = _factory.Services.GetRequiredService<IServer>();
        IServerAddressesFeature addresses = server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("No server address feature available.");

        BaseUrl = addresses.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("No server address available.");

        // Set up Playwright.
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    [OneTimeTearDown]
    public async Task GlobalTearDown()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates a new isolated browser context and page for a test.
    /// The caller is responsible for disposing the context after use.
    /// </summary>
    public static async Task<(IBrowserContext Context, IPage Page)> NewPageAsync()
    {
        if (_browser is null)
            throw new InvalidOperationException("Browser not initialised. Has OneTimeSetUp run?");

        IBrowserContext context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        IPage page = await context.NewPageAsync();
        return (context, page);
    }
}
