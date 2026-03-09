namespace UPMS.Ingestion;

using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUpmsIngestion(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ISnapshotIngestService, SnapshotIngestService>();
        return services;
    }
}
