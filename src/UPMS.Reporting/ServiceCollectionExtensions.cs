namespace UPMS.Reporting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UPMS.Reporting.Plugins;
using UPMS.Reporting.Plugins.Documents;
using UPMS.Reporting.Plugins.Email;
using UPMS.Reporting.Plugins.Examples;
using UPMS.Reporting.Plugins.PowerPoint;
using UPMS.Reporting.Plugins.Templates;
using UPMS.Reporting.Templates;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUpmsReporting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IReportTemplateStore>(_ =>
        {
            var rootPath = configuration["Artifacts:RootPath"]
                ?? Environment.GetEnvironmentVariable("UPMS_ARTIFACTS_ROOT")
                ?? "/var/lib/upms";

            var storagePath = configuration["ReportTemplates:StoragePath"]
                ?? Path.Combine(rootPath, "report-templates");

            return new FileSystemReportTemplateStore(storagePath);
        });

        services.AddScoped<IReportPlugin, StubReportPlugin>();
        services.AddScoped<IReportPlugin, PowerPointReportPlugin>();
        services.AddScoped<IReportPlugin, EmailNotificationPlugin>();
        services.AddScoped<IReportPlugin, StatusBreakdownReportPlugin>();
        services.AddScoped<IReportPlugin, TicketCsvExportReportPlugin>();
        services.AddScoped<IReportPlugin, FieldDeltaReportPlugin>();
        services.AddScoped<IReportPlugin, MonthEndLifecycleReportPlugin>();
        services.AddScoped<IReportPlugin, TicketDocumentExportPlugin>();
        services.AddScoped<IReportPlugin, TokenisedTemplateReportPlugin>();
        services.AddScoped<PluginRegistry>();
        services.AddScoped<IReportExecutionService, ReportExecutionService>();

        return services;
    }
}
