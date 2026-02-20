namespace UPMS.Web
{
    using Microsoft.Extensions.Options;
    using UPMS.Web.Components;
    using UPMS.Web.Plugins;
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

            // Register plugin registry (receives all IReportPlugin registrations via IEnumerable)
            builder.Services.AddSingleton<PluginRegistry>();

            // Register ingest service
            builder.Services.AddScoped<ISnapshotIngestService, SnapshotIngestService>();

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

            app.Run();
        }
    }
}
