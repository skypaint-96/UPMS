namespace UPMS.Web
{
    using System.IO;
    using Microsoft.AspNetCore.DataProtection;
    using Microsoft.Extensions.Options;
    using Npgsql;
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
            // Connection string: env var UPMS_CONNECTION_STRING takes priority,
            // falling back to ConnectionStrings:DefaultConnection, then
            // Database:ConnectionString in appsettings.json.
            // ------------------------------------------------------------------
            string? connectionString =
                Environment.GetEnvironmentVariable("UPMS_CONNECTION_STRING")
                ?? builder.Configuration.GetConnectionString("DefaultConnection")
                ?? builder.Configuration["Database:ConnectionString"];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "No database connection string found. Set the UPMS_CONNECTION_STRING " +
                    "environment variable or configure ConnectionStrings:DefaultConnection " +
                    "in appsettings.json.");
            }

            // Bind DatabaseOptions so injected services can read the connection string.
            builder.Services.Configure<DatabaseOptions>(opts =>
                opts.ConnectionString = connectionString);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // Register data service instance (wraps static TicketDataService)
            builder.Services.AddSingleton<TicketDataServiceInstance>();

            // Register canonical field mapping service (kept for backward compatibility)
            builder.Services.AddSingleton<IItsmFieldMappingService>(sp =>
                ItsmFieldMappingService.CreateFromOptions(
                    sp.GetRequiredService<IOptions<DatabaseOptions>>()));

            // Register ITSM source definition service
            builder.Services.AddSingleton<IItsmSourceService>(sp =>
                ItsmSourceService.CreateFromOptions(
                    sp.GetRequiredService<IOptions<DatabaseOptions>>()));

            // Register report plugins
            builder.Services.AddSingleton<IReportPlugin, StubReportPlugin>();
            builder.Services.AddSingleton<IReportPlugin, PowerPointReportPlugin>();
            builder.Services.AddSingleton<IReportPlugin, EmailNotificationPlugin>();

            // Register plugin registry (receives all IReportPlugin registrations via IEnumerable)
            builder.Services.AddSingleton<PluginRegistry>();

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
            // Initialise static TicketDataService with the resolved connection string.
            // ------------------------------------------------------------------
            TicketDataService.Initialize(connectionString);

            // ------------------------------------------------------------------
            // Run SQL migrations before accepting requests.
            // ------------------------------------------------------------------
            var logger = app.Services.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Migrations");

            await RunMigrationsAsync(connectionString, logger);

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

        // ----------------------------------------------------------------------
        // Migration runner
        // Applies *.sql files from sql/migrations/ in filename order.
        // Skips migrations already recorded in schema_migrations table.
        // ----------------------------------------------------------------------
        private static async Task RunMigrationsAsync(string connectionString, ILogger logger)
        {
            // Resolve the migrations directory relative to the app content root.
            // In the Docker image the files are copied to /app/sql/migrations/.
            // When running locally from the repo root they sit at sql/migrations/.
            string[] candidatePaths =
            [
                Path.Combine(AppContext.BaseDirectory, "sql", "migrations"),
                Path.Combine(Directory.GetCurrentDirectory(), "sql", "migrations"),
            ];

            string? migrationsDir = candidatePaths.FirstOrDefault(Directory.Exists);

            if (migrationsDir is null)
            {
                logger.LogWarning(
                    "Migration directory not found in any of: {Paths}. Skipping migrations.",
                    string.Join(", ", candidatePaths));
                return;
            }

            string[] migrationFiles = Directory
                .GetFiles(migrationsDir, "*.sql")
                .OrderBy(f => Path.GetFileName(f))
                .ToArray();

            if (migrationFiles.Length == 0)
            {
                logger.LogInformation("No migration files found in {Dir}.", migrationsDir);
                return;
            }

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Ensure the tracking table exists.
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS schema_migrations (
                        migration_name VARCHAR(255) PRIMARY KEY,
                        applied_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
                    );
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            foreach (string filePath in migrationFiles)
            {
                string migrationName = Path.GetFileName(filePath);

                // Check if already applied.
                bool alreadyApplied;
                await using (var checkCmd = connection.CreateCommand())
                {
                    checkCmd.CommandText =
                        "SELECT COUNT(1) FROM schema_migrations WHERE migration_name = @name";
                    checkCmd.Parameters.AddWithValue("name", migrationName);
                    long count = (long)(await checkCmd.ExecuteScalarAsync() ?? 0L);
                    alreadyApplied = count > 0;
                }

                if (alreadyApplied)
                {
                    logger.LogInformation("Migration {Name}: already applied, skipping.", migrationName);
                    continue;
                }

                string sql = await File.ReadAllTextAsync(filePath);

                await using var transaction = await connection.BeginTransactionAsync();
                try
                {
                    await using (var migCmd = connection.CreateCommand())
                    {
                        migCmd.Transaction = transaction;
                        migCmd.CommandText = sql;
                        await migCmd.ExecuteNonQueryAsync();
                    }

                    await using (var recordCmd = connection.CreateCommand())
                    {
                        recordCmd.Transaction = transaction;
                        recordCmd.CommandText =
                            "INSERT INTO schema_migrations (migration_name) VALUES (@name)";
                        recordCmd.Parameters.AddWithValue("name", migrationName);
                        await recordCmd.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    logger.LogInformation("Migration {Name}: applied successfully.", migrationName);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    logger.LogError(ex, "Migration {Name}: FAILED — rolled back.", migrationName);
                    throw;
                }
            }
        }
    }
}
