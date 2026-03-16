namespace UPMS.Data.Tests;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UPMS.Data.Delivery;

[TestFixture]
public sealed class DistributionListServiceTests
{
    private static UpmsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UpmsDbContext>()
            .UseInMemoryDatabase($"distribution-lists-{Guid.NewGuid():N}")
            .Options;

        return new UpmsDbContext(options);
    }

    [Test]
    public async Task CreateUpdateDelete_distribution_list_crud_flow_succeeds()
    {
        await using var context = CreateContext();
        var service = new DistributionListService(context);

        var created = await service.CreateAsync(
            new SaveDistributionListCommand(
                "Contoso",
                "Leadership",
                "Primary leadership recipients",
                true,
                [
                    new DistributionListRecipientInput("email", "one@example.com", "One", null, true, 0),
                    new DistributionListRecipientInput("email", "two@example.com", null, null, true, 1),
                ]),
            "tester");

        Assert.That(created.CompanyName, Is.EqualTo("Contoso"));
        Assert.That(created.CompanyKey, Is.EqualTo("contoso"));
        Assert.That(created.Recipients, Has.Count.EqualTo(2));
        Assert.That(created.ActiveRecipientCount, Is.EqualTo(2));

        var updated = await service.UpdateAsync(
            created.Id,
            new SaveDistributionListCommand(
                "Contoso",
                "Leadership",
                "Updated description",
                true,
                [
                    new DistributionListRecipientInput("email", "three@example.com", null, null, true, 0),
                ]),
            "editor");

        Assert.That(updated.Description, Is.EqualTo("Updated description"));
        Assert.That(updated.Recipients, Has.Count.EqualTo(1));
        Assert.That(updated.Recipients[0].Endpoint, Is.EqualTo("three@example.com"));
        Assert.That(updated.UpdatedBy, Is.EqualTo("editor"));

        await service.DeleteAsync(created.Id);

        var deleted = await service.GetByIdAsync(created.Id);
        Assert.That(deleted, Is.Null);
        Assert.That(await context.CompanyProfiles.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task CreateAsync_supports_multiple_lists_per_company()
    {
        await using var context = CreateContext();
        var service = new DistributionListService(context);

        var first = await service.CreateAsync(
            new SaveDistributionListCommand(
                "Contoso",
                "Leadership",
                null,
                true,
                [new DistributionListRecipientInput("email", "leader@example.com", null, null, true, 0)]),
            "tester");

        var second = await service.CreateAsync(
            new SaveDistributionListCommand(
                "Contoso",
                "Operations",
                null,
                true,
                [new DistributionListRecipientInput("email", "ops@example.com", null, null, true, 0)]),
            "tester");

        var companies = await service.GetCompaniesAsync();
        var company = companies.Single();
        var lists = await service.GetListsAsync("Contoso");

        Assert.That(first.CompanyProfileId, Is.EqualTo(second.CompanyProfileId));
        Assert.That(company.DistributionListCount, Is.EqualTo(2));
        Assert.That(lists.Select(list => list.Name).ToArray(), Is.EquivalentTo(new[] { "Leadership", "Operations" }));
    }


    [Test]
    public async Task GetCompaniesAsync_executes_on_relational_provider_and_returns_sorted_counts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<UpmsDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new UpmsDbContext(options);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE company_profile (
                id TEXT NOT NULL PRIMARY KEY,
                company_key TEXT NOT NULL,
                display_name TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            """);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE distribution_list (
                id TEXT NOT NULL PRIMARY KEY,
                company_profile_id TEXT NOT NULL,
                name TEXT NOT NULL,
                description TEXT NULL,
                is_active INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                created_by TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                updated_by TEXT NULL,
                FOREIGN KEY(company_profile_id) REFERENCES company_profile(id) ON DELETE CASCADE
            );
            """);

        var utcNow = DateTime.UtcNow;
        var alphaId = Guid.NewGuid();
        var bravoId = Guid.NewGuid();

        context.CompanyProfiles.AddRange(
            new CompanyProfile
            {
                Id = alphaId,
                CompanyKey = "alpha",
                DisplayName = "Alpha",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            },
            new CompanyProfile
            {
                Id = bravoId,
                CompanyKey = "bravo",
                DisplayName = "Bravo",
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            });

        context.DistributionLists.AddRange(
            new DistributionList
            {
                Id = Guid.NewGuid(),
                CompanyProfileId = bravoId,
                Name = "Leadership",
                IsActive = true,
                CreatedAt = utcNow,
                CreatedBy = "tester",
                UpdatedAt = utcNow,
                UpdatedBy = "tester"
            },
            new DistributionList
            {
                Id = Guid.NewGuid(),
                CompanyProfileId = alphaId,
                Name = "Leadership",
                IsActive = true,
                CreatedAt = utcNow,
                CreatedBy = "tester",
                UpdatedAt = utcNow,
                UpdatedBy = "tester"
            },
            new DistributionList
            {
                Id = Guid.NewGuid(),
                CompanyProfileId = alphaId,
                Name = "Operations",
                IsActive = true,
                CreatedAt = utcNow,
                CreatedBy = "tester",
                UpdatedAt = utcNow,
                UpdatedBy = "tester"
            });

        await context.SaveChangesAsync();

        var service = new DistributionListService(context);
        var companies = await service.GetCompaniesAsync();

        Assert.That(companies.Select(company => company.DisplayName).ToArray(), Is.EqualTo(new[] { "Alpha", "Bravo" }));
        Assert.That(companies.Single(company => company.DisplayName == "Alpha").DistributionListCount, Is.EqualTo(2));
        Assert.That(companies.Single(company => company.DisplayName == "Bravo").DistributionListCount, Is.EqualTo(1));
    }

}
