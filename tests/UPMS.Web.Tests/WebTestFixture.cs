namespace UPMS.Web.Tests;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Playwright;
using UPMS.Data;
using UPMS.Web.Components;

/// <summary>
/// Starts the UPMS.Web application on a real Kestrel port and provides
/// Playwright browser/page instances for end-to-end tests.
/// </summary>
[SetUpFixture]
public class WebTestFixture
{
    private static WebApplication? _app;
    private static IPlaywright? _playwright;
    private static IBrowser? _browser;

    /// <summary>
    /// Base URL of the running web application (e.g. "https://localhost:5001").
    /// </summary>
    public static string BaseUrl { get; private set; } = string.Empty;

    [OneTimeSetUp]
    public async Task GlobalSetUp()
    {
        // Resolve the UPMS.Web project directory so that Razor components,
        // static assets, and appsettings.json are discovered correctly.
        string webProjectDir = FindWebProjectDirectory();

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = webProjectDir,
            WebRootPath = Path.Combine(webProjectDir, "wwwroot")
        });

        builder.WebHost.UseUrls("https://127.0.0.1:0");

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.Configure<DatabaseOptions>(options =>
            options.ConnectionString = "Host=localhost;Database=upms_test_placeholder;");
        builder.Services.AddSingleton<TicketDataServiceInstance>();

        _app = builder.Build();

        _app.UseStatusCodePagesWithReExecute("/not-found");
        _app.UseHttpsRedirection();
        _app.UseStaticFiles();
        _app.UseAntiforgery();
        _app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        await _app.StartAsync();

        IServer server = _app.Services.GetRequiredService<IServer>();
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

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
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

    /// <summary>
    /// Walks up from the test assembly's directory to locate the UPMS.Web project folder.
    /// </summary>
    private static string FindWebProjectDirectory()
    {
        // Start from the solution root (tests/UPMS.Web.Tests/bin/Debug/net10.0 ? walk up 5 levels)
        string startDir = AppContext.BaseDirectory;
        DirectoryInfo? dir = new(startDir);

        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "src", "UPMS.Web");
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "UPMS.Web.csproj")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the UPMS.Web project directory. Started searching from: {startDir}");
    }
}
