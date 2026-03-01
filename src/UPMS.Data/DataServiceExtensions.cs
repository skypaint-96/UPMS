namespace UPMS.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Extension methods for registering UPMS data-layer services with the DI container.
/// </summary>
public static class DataServiceExtensions
{
    /// <summary>
    /// Registers all UPMS data services:
    /// <list type="bullet">
    ///   <item><description><see cref="UpmsDbContext"/> via Npgsql EF Core provider</description></item>
    ///   <item><description><see cref="ICommandRepository"/> → <see cref="EfCommandRepository"/> (Scoped)</description></item>
    ///   <item><description><see cref="IReadModelService"/> → <see cref="EfReadModelService"/> (Scoped)</description></item>
    ///   <item><description><see cref="IItsmSourceService"/> → <see cref="ItsmSourceService"/> (kept as Singleton, Dapper)</description></item>
    ///   <item><description><see cref="IItsmFieldMappingService"/> → <see cref="ItsmFieldMappingService"/> (kept as Singleton, Dapper)</description></item>
    /// </list>
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">Application configuration used to resolve the connection string.</param>
    /// <returns>The original <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddUpmsData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Resolve connection string: env var → ConnectionStrings:DefaultConnection → Database:ConnectionString
        string connectionString =
            Environment.GetEnvironmentVariable("UPMS_CONNECTION_STRING")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? configuration["Database:ConnectionString"]
            ?? throw new InvalidOperationException(
                "No database connection string found. Set the UPMS_CONNECTION_STRING " +
                "environment variable or configure ConnectionStrings:DefaultConnection " +
                "in appsettings.json.");

        // ── EF Core (Npgsql) ───────────────────────────────────────────────
        services.AddDbContext<UpmsDbContext>(options =>
            options.UseNpgsql(connectionString));

        // ── EF-backed write repository ─────────────────────────────────────
        services.AddScoped<ICommandRepository, EfCommandRepository>();

        // ── EF-backed read model service ───────────────────────────────────
        services.AddScoped<IReadModelService, EfReadModelService>();

        // ── Dapper-backed services (kept until Phase 3) ────────────────────
        // DatabaseOptions must already be configured by the caller (Program.cs does this).
        services.AddSingleton<IItsmSourceService>(sp =>
            ItsmSourceService.CreateFromOptions(
                sp.GetRequiredService<IOptions<DatabaseOptions>>()));

        services.AddSingleton<IItsmFieldMappingService>(sp =>
            ItsmFieldMappingService.CreateFromOptions(
                sp.GetRequiredService<IOptions<DatabaseOptions>>()));

        return services;
    }
}
