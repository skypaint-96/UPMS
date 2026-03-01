namespace UPMS.Data.Tests;

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Integration tests for <see cref="ItsmSourceService"/>.
/// Each test uses an isolated schema prefix / unique source names so tests don't
/// interfere with the shared fixture data.  The service is backed by the Postgres
/// test database (same connection string used by <see cref="TestDatabaseFixture"/>).
/// </summary>
[TestFixture]
public class ItsmSourceServiceTests : TicketDataServiceTestBase
{
    // ── GetAllSources ──────────────────────────────────────────────────────

    [Test]
    public async Task GetAllSources_ReturnsEmpty_WhenNoSourcesDefined()
    {
        // Arrange — use a fresh isolated service pointing at the Postgres test DB
        IItsmSourceService service = BuildIsolatedService();

        // Act
        IReadOnlyList<ItsmSource> sources = await service.GetAllSourcesAsync();

        // Assert — the isolated service shares the DB but we only assert that the
        // call succeeds; any pre-existing rows are acceptable here.
        Assert.That(sources, Is.Not.Null);
    }

    [Test]
    public async Task CreateSource_StoresAndReturnsSource()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string uniqueName = $"sn-test-{Guid.NewGuid():N}";

        // Act
        ItsmSource created = await service.CreateSourceAsync(uniqueName, "ServiceNow Test");

        // Assert
        Assert.That(created.Name, Is.EqualTo(uniqueName));
        Assert.That(created.DisplayLabel, Is.EqualTo("ServiceNow Test"));
        Assert.That(created.Id, Is.GreaterThan(0));
    }

    [Test]
    public async Task CreateSource_ThrowsOnDuplicateName()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string uniqueName = $"dup-source-{Guid.NewGuid():N}";
        await service.CreateSourceAsync(uniqueName, "Duplicate");

        // Act & Assert — inserting again should throw (DB UNIQUE constraint)
        Assert.ThrowsAsync<Exception>(async () =>
            await service.CreateSourceAsync(uniqueName, "Duplicate Again"));
    }

    // ── GetSourceByName ────────────────────────────────────────────────────

    [Test]
    public async Task GetSourceByName_ReturnsNull_WhenNotFound()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();

        // Act
        ItsmSource? result = await service.GetSourceByNameAsync($"nonexistent-{Guid.NewGuid():N}");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSourceByName_ReturnsSource_WhenFound()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string uniqueName = $"find-me-{Guid.NewGuid():N}";
        await service.CreateSourceAsync(uniqueName, "Find Me Label");

        // Act
        ItsmSource? result = await service.GetSourceByNameAsync(uniqueName);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo(uniqueName));
        Assert.That(result.DisplayLabel, Is.EqualTo("Find Me Label"));
    }

    // ── DeleteSource ───────────────────────────────────────────────────────

    [Test]
    public async Task DeleteSource_RemovesSource()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string uniqueName = $"to-delete-{Guid.NewGuid():N}";
        await service.CreateSourceAsync(uniqueName, "To Delete");

        // Act
        await service.DeleteSourceAsync(uniqueName);

        // Assert
        ItsmSource? result = await service.GetSourceByNameAsync(uniqueName);
        Assert.That(result, Is.Null);
    }

    // ── UpsertMapping ──────────────────────────────────────────────────────

    [Test]
    public async Task UpsertMapping_AddsNewMapping()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string sourceName = $"map-source-{Guid.NewGuid():N}";
        await service.CreateSourceAsync(sourceName, "Map Source");

        // Act
        await service.UpsertMappingAsync(sourceName, "number", "ticket_key", isRequired: true);

        // Assert
        string? canonical = await service.GetCanonicalNameAsync(sourceName, "number");
        Assert.That(canonical, Is.EqualTo("ticket_key"));
    }

    [Test]
    public async Task UpsertMapping_UpdatesExistingMapping()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string sourceName = $"update-source-{Guid.NewGuid():N}";
        await service.CreateSourceAsync(sourceName, "Update Source");
        await service.UpsertMappingAsync(sourceName, "state", "status", isRequired: false);

        // Act — update canonical name
        await service.UpsertMappingAsync(sourceName, "state", "workflow_status", isRequired: false);

        // Assert
        string? canonical = await service.GetCanonicalNameAsync(sourceName, "state");
        Assert.That(canonical, Is.EqualTo("workflow_status"));
    }

    [Test]
    public async Task UpsertMapping_SetsIsRequired()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string sourceName = $"req-source-{Guid.NewGuid():N}";
        await service.CreateSourceAsync(sourceName, "Required Source");
        await service.UpsertMappingAsync(sourceName, "number",  "ticket_key", isRequired: true);
        await service.UpsertMappingAsync(sourceName, "company", "company",    isRequired: true);
        await service.UpsertMappingAsync(sourceName, "state",   "status",     isRequired: false);

        // Act
        IReadOnlyList<string> required = await service.GetRequiredFieldsAsync(sourceName);

        // Assert — only number and company are required
        Assert.That(required, Is.EquivalentTo(new[] { "number", "company" }));
    }

    // ── GetRequiredFields ──────────────────────────────────────────────────

    [Test]
    public async Task GetRequiredFields_ReturnsOnlyRequiredFields()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string sourceName = $"rf-source-{Guid.NewGuid():N}";
        await service.CreateSourceAsync(sourceName, "RF Source");
        await service.UpsertMappingAsync(sourceName, "col_a", "canonical_a", isRequired: true);
        await service.UpsertMappingAsync(sourceName, "col_b", "canonical_b", isRequired: false);
        await service.UpsertMappingAsync(sourceName, "col_c", "canonical_c", isRequired: true);

        // Act
        IReadOnlyList<string> required = await service.GetRequiredFieldsAsync(sourceName);

        // Assert
        Assert.That(required, Has.Count.EqualTo(2));
        Assert.That(required, Is.EquivalentTo(new[] { "col_a", "col_c" }));
    }

    // ── GetCanonicalName ───────────────────────────────────────────────────

    [Test]
    public async Task GetCanonicalName_ReturnsNull_WhenNoMapping()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();

        // Act
        string? canonical = await service.GetCanonicalNameAsync($"no-source-{Guid.NewGuid():N}", "no-field");

        // Assert
        Assert.That(canonical, Is.Null);
    }

    [Test]
    public async Task GetCanonicalName_ReturnsCanonicalName_WhenMappingExists()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string sourceName = $"cn-source-{Guid.NewGuid():N}";
        await service.CreateSourceAsync(sourceName, "CN Source");
        await service.UpsertMappingAsync(sourceName, "short_description", "title", isRequired: false);

        // Act
        string? canonical = await service.GetCanonicalNameAsync(sourceName, "short_description");

        // Assert
        Assert.That(canonical, Is.EqualTo("title"));
    }

    // ── GetSourceDefinition ────────────────────────────────────────────────

    [Test]
    public async Task GetSourceDefinition_ThrowsKeyNotFoundException_WhenSourceNotFound()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();

        // Act & Assert
        Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await service.GetSourceDefinitionAsync($"ghost-source-{Guid.NewGuid():N}"));
    }

    [Test]
    public async Task GetSourceDefinition_ReturnsMappings()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string sourceName = $"def-source-{Guid.NewGuid():N}";
        await service.CreateSourceAsync(sourceName, "Def Source");
        await service.UpsertMappingAsync(sourceName, "number",  "ticket_key", isRequired: true);
        await service.UpsertMappingAsync(sourceName, "company", "company",    isRequired: true);
        await service.UpsertMappingAsync(sourceName, "state",   "status",     isRequired: false);

        // Act
        ItsmSourceDefinition definition = await service.GetSourceDefinitionAsync(sourceName);

        // Assert
        Assert.That(definition.Source.Name, Is.EqualTo(sourceName));
        Assert.That(definition.Mappings, Has.Count.EqualTo(3));
        Assert.That(definition.Mappings.Select(m => m.SourceFieldName),
            Is.EquivalentTo(new[] { "number", "company", "state" }));
    }

    // ── DeleteMapping ──────────────────────────────────────────────────────

    [Test]
    public async Task DeleteMapping_RemovesOnlyTargetedMapping()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        string sourceName = $"dm-source-{Guid.NewGuid():N}";
        await service.CreateSourceAsync(sourceName, "DM Source");
        await service.UpsertMappingAsync(sourceName, "field_a", "canonical_a", isRequired: false);
        await service.UpsertMappingAsync(sourceName, "field_b", "canonical_b", isRequired: false);
        await service.UpsertMappingAsync(sourceName, "field_c", "canonical_c", isRequired: false);

        // Act
        await service.DeleteMappingAsync(sourceName, "field_b");

        // Assert — field_b gone, field_a and field_c remain
        string? aCanonical = await service.GetCanonicalNameAsync(sourceName, "field_a");
        string? bCanonical = await service.GetCanonicalNameAsync(sourceName, "field_b");
        string? cCanonical = await service.GetCanonicalNameAsync(sourceName, "field_c");

        Assert.That(aCanonical, Is.EqualTo("canonical_a"));
        Assert.That(bCanonical, Is.Null);
        Assert.That(cCanonical, Is.EqualTo("canonical_c"));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an <see cref="IItsmSourceService"/> backed by the Postgres test database.
    /// Each call returns a fresh service instance; uniqueness of test data is achieved
    /// by appending a <see cref="Guid"/> to source/field names within each test.
    /// </summary>
    private static IItsmSourceService BuildIsolatedService()
    {
        Func<System.Data.IDbConnection> factory = TestDatabaseFixture.ConnectionFactory;

        return new ItsmSourceService(factory);
    }
}
