namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UPMS.Data.Artifacts;
using UPMS.Data.Jobs;
using UPMS.Data.ProblemRequests;

/// <summary>
/// Extension methods for registering UPMS data-layer services with the DI container.
/// </summary>
public static class DataServiceExtensions
{
    public static IServiceCollection AddUpmsData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("UPMS_CONNECTION_STRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? configuration["Database:ConnectionString"]
            ?? throw new InvalidOperationException(
                "No database connection string found. Set the UPMS_CONNECTION_STRING " +
                "environment variable or configure ConnectionStrings:DefaultConnection " +
                "in appsettings.json.");

        services.AddDbContext<UpmsDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.Configure<ArtifactStorageOptions>(configuration.GetSection("Artifacts"));
        services.PostConfigure<ArtifactStorageOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.RootPath))
            {
                options.RootPath = configuration["Artifacts:RootPath"]
                    ?? Environment.GetEnvironmentVariable("UPMS_ARTIFACTS_ROOT")
                    ?? "/var/lib/upms";
            }
        });

        services.AddScoped<ICommandRepository, EfCommandRepository>();
        services.AddScoped<IReadModelService, EfReadModelService>();
        services.AddScoped<IItsmSourceService, ItsmSourceService>();
        services.AddScoped<IItsmFieldMappingService, ItsmFieldMappingService>();
        services.AddScoped<ICanonicalFieldService, CanonicalFieldService>();
        services.AddScoped<UpmsSchemaBootstrapper>();
        services.AddScoped<TicketDataService>();
        services.AddScoped<TicketDataServiceInstance>();
        services.AddScoped<IBackgroundJobService, BackgroundJobService>();
        services.AddScoped<IProblemRequestService, ProblemRequestService>();
        services.AddSingleton<IArtifactStorage, FileSystemArtifactStorage>();

        return services;
    }
}
