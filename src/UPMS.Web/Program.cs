namespace UPMS.Web
{
    using System.IO;
    using System.Security.Claims;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authentication.OpenIdConnect;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Microsoft.Identity.Web;
    using UPMS.Web.Components;
    using UPMS.Web.Plugins;
    using UPMS.Web.Plugins.PowerPoint;
    using UPMS.Web.Plugins.Email;
    using UPMS.Web.Services;
    using UPMS.Data;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ------------------------------------------------------------------
            // Authentication mode. Can be overridden via environment variable
            // or appsettings. In Development the default is permissive (None).
            // Auth:Mode = "Entra" to enable Azure AD (production).
            // ------------------------------------------------------------------
            string authMode = builder.Configuration["Auth:Mode"] ?? (builder.Environment.IsDevelopment() ? "None" : "Entra");

            // Support reading secrets provided as files under /run/secrets. These are
            // mapped by docker-compose to secret names. If present, inject them into
            // configuration so Microsoft.Identity.Web can pick them up.
            string? upmsConnSecretFile = Environment.GetEnvironmentVariable("UPMS_CONNECTION_STRING_FILE") ?? builder.Configuration["UPMS_CONNECTION_STRING_FILE"] ?? builder.Configuration["UPMS:ConnectionStringFile"];
            if (!string.IsNullOrWhiteSpace(upmsConnSecretFile) && File.Exists(upmsConnSecretFile))
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ConnectionString"] = File.ReadAllText(upmsConnSecretFile).Trim()
                });
            }

            string? azureClientSecretFile = Environment.GetEnvironmentVariable("AzureAd__ClientSecret_FILE") ?? builder.Configuration["AzureAd:ClientSecretFile"];
            if (!string.IsNullOrWhiteSpace(azureClientSecretFile) && File.Exists(azureClientSecretFile))
            {
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AzureAd:ClientSecret"] = File.ReadAllText(azureClientSecretFile).Trim()
                });
            }

            // Register IHttpContextAccessor for services that need to read the current user.
            builder.Services.AddHttpContextAccessor();

            // If authentication is enabled, register Microsoft Identity Web OpenID Connect
            if (!string.Equals(authMode, "None", StringComparison.OrdinalIgnoreCase))
            {
                // NOTE: Ensure Microsoft.Identity.Web is added to the project NuGet packages.
                // Reads configuration from configuration section "AzureAd".
                builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

                // Authorization: require membership of a configured Azure AD group when set.
                string? requiredGroupId = builder.Configuration["AzureAd:RequiredGroupId"];
                builder.Services.AddAuthorization(options =>
                {
                    if (!string.IsNullOrWhiteSpace(requiredGroupId))
                    {
                        options.AddPolicy("RequireAzureGroup", policy =>
                            policy.RequireAssertion(context =>
                            {
                                if (!(context.User?.Identity?.IsAuthenticated ?? false))
                                    return false;

                                var groups = context.User.FindAll("groups").Select(c => c.Value).ToList();
                                // Some tokens use the full claim URI; include that as a fallback.
                                groups.AddRange(context.User.FindAll("http://schemas.microsoft.com/identity/claims/groups").Select(c => c.Value));

                                return groups.Contains(requiredGroupId);
                            }));
                    }
                });
            }

            // ------------------------------------------------------------------
            // Connection string: env var UPMS_CONNECTION_STRING takes priority,
            // falling back to ConnectionStrings:DefaultConnection, then
            // Database:ConnectionString in appsettings.json.
            // Also support reading from a secret file at /run/secrets/upms_connection_string
            // ------------------------------------------------------------------
            string? connectionString =
                Environment.GetEnvironmentVariable("UPMS_CONNECTION_STRING")
                ?? builder.Configuration.GetConnectionString("DefaultConnection")
                ?? builder.Configuration["Database:ConnectionString"]
                ?? (Environment.GetEnvironmentVariable("UPMS_CONNECTION_STRING_FILE") is string f && File.Exists(f) ? File.ReadAllText(f).Trim() : null)
                ?? (File.Exists("/run/secrets/upms_connection_string") ? File.ReadAllText("/run/secrets/upms_connection_string").Trim() : null);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "No database connection string found. Set the UPMS_CONNECTION_STRING environment variable, provide UPMS_CONNECTION_STRING_FILE pointing to a secret file, or configure ConnectionStrings:DefaultConnection in appsettings.json.");
            }


            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // Register EF Core + Dapper data services (ICommandRepository, IReadModelService,
            // IItsmSourceService, IItsmFieldMappingService).
            builder.Services.AddUpmsData(builder.Configuration);

            // Register data service instance (wraps static TicketDataService — kept until Phase 3)

            // Register report plugins (Scoped: plugins consume TicketDataServiceInstance which is Scoped)
            builder.Services.AddScoped<IReportPlugin, StubReportPlugin>();
            builder.Services.AddScoped<IReportPlugin, PowerPointReportPlugin>();
            builder.Services.AddScoped<IReportPlugin, EmailNotificationPlugin>();

            // Register plugin registry (Scoped: receives IEnumerable<IReportPlugin> which are Scoped)
            builder.Services.AddScoped<PluginRegistry>();

            // Register ingest service
            builder.Services.AddScoped<ISnapshotIngestService, SnapshotIngestService>();

            // Register report download store (scoped per-connection)
            builder.Services.AddScoped<ReportDownloadStore>();

            // ------------------------------------------------------------------
            // Data Protection: persist keys to a directory so they survive
            // container restarts. In Docker the directory is backed by the
            // named volume 'upms_keys' mounted at /app/keys.
            // ------------------------------------------------------------------
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo("/app/keys"))
                .SetApplicationName("UPMS");

            var app = builder.Build();

            // ------------------------------------------------------------------
            // Initialise migration runner using EF Core migrations
            // ------------------------------------------------------------------
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<UpmsDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                try
                {
                    logger.LogInformation("Applying EF Core migrations...");
                    await db.Database.MigrateAsync();
                    logger.LogInformation("Migrations applied successfully.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to apply database migrations. Application startup aborted.");
                    throw;
                }
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

            // Only redirect to HTTPS when a certificate / HTTPS port is actually
            // configured (i.e. local development). Inside Docker the container
            // runs plain HTTP behind a reverse proxy, so redirecting would loop
            // forever and also causes the "Failed to determine the https port"
            // warning in logs.
            if (app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseAntiforgery();

            // Logging / correlation middleware: attach X-Request-Id and enrich logs with
            // correlation id and acting user where available. Uses logging scopes so
            // structured log sinks (Azure Monitor / Log Analytics) can correlate entries.
            app.Use(async (ctx, next) =>
            {
                var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();

                string correlationId = ctx.Request.Headers["X-Request-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString();
                ctx.Response.Headers["X-Request-Id"] = correlationId;

                string actor = "anonymous";
                if (ctx.User?.Identity?.IsAuthenticated ?? false)
                {
                    actor = ctx.User.FindFirst("preferred_username")?.Value
                            ?? ctx.User.FindFirst(ClaimTypes.Upn)?.Value
                            ?? ctx.User.FindFirst("name")?.Value
                            ?? ctx.User.FindFirst("oid")?.Value
                            ?? "authenticated";
                }

                using (logger.BeginScope(new Dictionary<string, object>
                {
                    ["CorrelationId"] = correlationId,
                    ["Actor"] = actor
                }))
                {
                    await next();
                }
            });

            // Authentication / Authorization middleware - only enabled when not in permissive mode.
            if (!string.Equals(authMode, "None", StringComparison.OrdinalIgnoreCase))
            {
                app.UseAuthentication();
                app.UseAuthorization();

                // In Production enforce sign-in and membership of the configured group (403 otherwise).
                if (!app.Environment.IsDevelopment())
                {
                    string? requiredGroupId = builder.Configuration["AzureAd:RequiredGroupId"];
                    app.Use(async (ctx, next) =>
                    {
                        if (!(ctx.User?.Identity?.IsAuthenticated ?? false))
                        {
                            // Trigger challenge (redirect to identity provider)
                            await ctx.ChallengeAsync();
                            return;
                        }

                        if (!string.IsNullOrWhiteSpace(requiredGroupId))
                        {
                            var groups = ctx.User.FindAll("groups").Select(c => c.Value)
                                .Concat(ctx.User.FindAll("http://schemas.microsoft.com/identity/claims/groups").Select(c => c.Value));

                            if (!groups.Contains(requiredGroupId))
                            {
                                ctx.Response.StatusCode = 403;
                                await ctx.Response.WriteAsync("Forbidden: user not in required Azure AD group.");
                                return;
                            }
                        }

                        await next();
                    });
                }
            }

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.MapGet("/reports/download", async (ReportDownloadStore store, HttpContext ctx) =>
            {
                if (store.PendingDownload is null || store.PendingDownload.FileContent is null)
                {
                    ctx.Response.StatusCode = 404;
                    return;
                }
                var result = store.PendingDownload;
                store.PendingDownload = null;
                ctx.Response.ContentType = result.ContentType ?? "application/octet-stream";
                ctx.Response.Headers.ContentDisposition = $"attachment; filename=\"{result.FileName ?? "report"}\"";
                await ctx.Response.Body.WriteAsync(result.FileContent);
            });

            app.Run();
        }
    }
}
