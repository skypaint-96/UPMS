namespace UPMS.Web
{
    using Microsoft.Extensions.Options;
    using UPMS.Web.Components;
    using UPMS.Web.Plugins;
    using UPMS.Web.Plugins.PowerPoint;
    using UPMS.Web.Plugins.Email;
    using UPMS.Web.Services;
    using UPMS.Data;

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            // Register database options and data service
            builder.Services.Configure<DatabaseOptions>(
                builder.Configuration.GetSection(DatabaseOptions.SectionName));
            builder.Services.AddSingleton<TicketDataServiceInstance>();

            // Register canonical field mapping service
            builder.Services.AddSingleton<IItsmFieldMappingService>(sp =>
                ItsmFieldMappingService.CreateFromOptions(
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

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();

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
    }
}
