namespace UPMS.Api.Tests;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UPMS.Data;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["Auth:Mode"] = "None",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=ignored;Username=ignored;Password=ignored",
                ["Artifacts:RootPath"] = Path.Combine(Path.GetTempPath(), "upms-api-tests-artifacts")
            };

            config.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<UpmsDbContext>>();
            services.AddDbContext<UpmsDbContext>(options =>
                options.UseInMemoryDatabase($"upms-api-tests-{Guid.NewGuid():N}"));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<UpmsDbContext>();
            db.Database.EnsureCreated();
            SeedAsync(db).GetAwaiter().GetResult();
        });
    }

    private static async Task SeedAsync(UpmsDbContext db)
    {
        if (await db.Snapshots.AnyAsync())
            return;

        var snapshotId = Guid.NewGuid();
        var ticketKey = TicketKeyFactory.Compose("servicenow-prod", "Contoso", "PRB0001");

        db.ItsmSources.Add(new ItsmSource
        {
            Id = 1,
            Name = "servicenow-prod",
            DisplayLabel = "ServiceNow Prod"
        });

        db.ItsmFieldMappings.AddRange(
            new ItsmFieldMapping
            {
                ItsmSource = "servicenow-prod",
                SourceFieldName = "number",
                CanonicalFieldName = "Number",
                IsRequired = true
            },
            new ItsmFieldMapping
            {
                ItsmSource = "servicenow-prod",
                SourceFieldName = "company",
                CanonicalFieldName = "Company",
                IsRequired = true
            },
            new ItsmFieldMapping
            {
                ItsmSource = "servicenow-prod",
                SourceFieldName = "state",
                CanonicalFieldName = "State",
                IsRequired = false
            });

        db.Snapshots.Add(new Snapshot
        {
            Id = snapshotId,
            ItsmSource = "servicenow-prod",
            SnapshotDate = new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc),
            UploadedBy = "test",
            UploadedAt = new DateTime(2026, 03, 01, 1, 0, 0, DateTimeKind.Utc)
        });

        db.SnapshotTickets.Add(new SnapshotTicket
        {
            Id = Guid.NewGuid(),
            SnapshotId = snapshotId,
            CompanyName = "Contoso",
            TicketKey = ticketKey
        });

        db.FieldChanges.AddRange(
            new FieldChange
            {
                CompanyName = "Contoso",
                TicketKey = ticketKey,
                FieldName = "number",
                CanonicalFieldName = "Number",
                FieldValue = "PRB0001",
                ObservedAt = new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc),
                SnapshotId = snapshotId
            },
            new FieldChange
            {
                CompanyName = "Contoso",
                TicketKey = ticketKey,
                FieldName = "company",
                CanonicalFieldName = "Company",
                FieldValue = "Contoso",
                ObservedAt = new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc),
                SnapshotId = snapshotId
            },
            new FieldChange
            {
                CompanyName = "Contoso",
                TicketKey = ticketKey,
                FieldName = "state",
                CanonicalFieldName = "State",
                FieldValue = "Investigating",
                ObservedAt = new DateTime(2026, 03, 01, 0, 0, 0, DateTimeKind.Utc),
                SnapshotId = snapshotId
            });

        foreach (var definition in CanonicalFieldDefaults.All)
        {
            db.CanonicalFieldDefinitions.Add(new CanonicalFieldDefinition
            {
                Name = definition.Name,
                DataType = definition.DataType,
                IsSystemRequired = definition.IsSystemRequired
            });
        }

        await db.SaveChangesAsync();
    }
}
