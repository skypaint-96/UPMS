namespace UPMS.Reporting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UPMS.Reporting.Plugins;
using UPMS.Reporting.Plugins.Templates;
using UPMS.Reporting.Delivery;
using UPMS.Reporting.Templates;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUpmsReporting(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IReportTemplateTypeProvider, DefaultReportTemplateTypeProvider>();
        services.AddSingleton<ReportTemplateTypeRegistry>();
        services.AddSingleton<IReportTemplateStore>(sp =>
        {
            var rootPath = configuration["Artifacts:RootPath"]
                ?? Environment.GetEnvironmentVariable("UPMS_ARTIFACTS_ROOT")
                ?? "/var/lib/upms";

            var storagePath = configuration["ReportTemplates:StoragePath"]
                ?? Path.Combine(rootPath, "report-templates");

            var typeRegistry = sp.GetRequiredService<ReportTemplateTypeRegistry>();
            return new FileSystemReportTemplateStore(storagePath, typeRegistry);
        });
        services.AddSingleton<IReportTemplateApplicabilityService, ReportTemplateApplicabilityService>();
        services.AddSingleton<IReportTemplateBootstrapper, ReportTemplateBootstrapper>();

        services.Configure<EmailDeliveryOptions>(configuration.GetSection("Delivery:Email"));
        services.AddScoped<IEmailReportDeliverySender, SmtpEmailReportDeliverySender>();
        services.AddScoped<IReportDeliveryWorkflowService, ReportDeliveryWorkflowService>();

        services.AddScoped<IReportPlugin, TokenisedTemplateReportPlugin>();
        services.AddScoped<PluginRegistry>();
        services.AddScoped<IReportExecutionService, ReportExecutionService>();

        return services;
    }
}
