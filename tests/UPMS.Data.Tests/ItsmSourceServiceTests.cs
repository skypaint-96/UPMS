namespace UPMS.Data.Tests;

using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Integration tests for <see cref="ItsmSourceService"/>.
/// All tests use the shared SQLite (or PostgreSQL) test database.
/// </summary>
[TestFixture]
public class ItsmSourceServiceTests : TicketDataServiceTestBase
{
    // ── GetAllSources ──────────────────────────────────────────────────────

    [Test]
    public async Task GetAllSources_ReturnsEmpty_WhenNoSourcesDefined()
    {
        // Arrange — use a fresh isolated service with an empty DB
        IItsmSourceService service = BuildIsolatedService();

        // Act
        IReadOnlyList<ItsmSource> sources = await service.GetAllSourcesAsync();

        // Assert
        Assert.That(sources, Is.Empty);
    }

    [Test]
    public async Task CreateSource_StoresAndReturnsSource()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();

        // Act
        ItsmSource created = await service.CreateSourceAsync("sn-test", "ServiceNow Test");

        // Assert
        Assert.That(created.Name, Is.EqualTo("sn-test"));
        Assert.That(created.DisplayLabel, Is.EqualTo("ServiceNow Test"));
        Assert.That(created.Id, Is.GreaterThan(0));
    }

    [Test]
    public async Task CreateSource_ThrowsOnDuplicateName()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        await service.CreateSourceAsync("dup-source", "Duplicate");

        // Act & Assert — inserting again should throw (DB UNIQUE constraint)
        Assert.ThrowsAsync<Exception>(async () =>
            await service.CreateSourceAsync("dup-source", "Duplicate Again"));
    }

    // ── GetSourceByName ────────────────────────────────────────────────────

    [Test]
    public async Task GetSourceByName_ReturnsNull_WhenNotFound()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();

        // Act
        ItsmSource? result = await service.GetSourceByNameAsync("nonexistent-source");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetSourceByName_ReturnsSource_WhenFound()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        await service.CreateSourceAsync("find-me", "Find Me Label");

        // Act
        ItsmSource? result = await service.GetSourceByNameAsync("find-me");

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Name, Is.EqualTo("find-me"));
        Assert.That(result.DisplayLabel, Is.EqualTo("Find Me Label"));
    }

    // ── DeleteSource ───────────────────────────────────────────────────────

    [Test]
    public async Task DeleteSource_RemovesSource()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        await service.CreateSourceAsync("to-delete", "To Delete");

        // Act
        await service.DeleteSourceAsync("to-delete");

        // Assert
        ItsmSource? result = await service.GetSourceByNameAsync("to-delete");
        Assert.That(result, Is.Null);
    }

    // ── UpsertMapping ──────────────────────────────────────────────────────

    [Test]
    public async Task UpsertMapping_AddsNewMapping()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        await service.CreateSourceAsync("map-source", "Map Source");

        // Act
        await service.UpsertMappingAsync("map-source", "number", "ticket_key", isRequired: true);

        // Assert
        string? canonical = await service.GetCanonicalNameAsync("map-source", "number");
        Assert.That(canonical, Is.EqualTo("ticket_key"));
    }

    [Test]
    public async Task UpsertMapping_UpdatesExistingMapping()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        await service.CreateSourceAsync("update-source", "Update Source");
        await service.UpsertMappingAsync("update-source", "state", "status", isRequired: false);

        // Act — update canonical name
        await service.UpsertMappingAsync("update-source", "state", "workflow_status", isRequired: false);

        // Assert
        string? canonical = await service.GetCanonicalNameAsync("update-source", "state");
        Assert.That(canonical, Is.EqualTo("workflow_status"));
    }

    [Test]
    public async Task UpsertMapping_SetsIsRequired()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        await service.CreateSourceAsync("req-source", "Required Source");
        await service.UpsertMappingAsync("req-source", "number",  "ticket_key", isRequired: true);
        await service.UpsertMappingAsync("req-source", "company", "company",    isRequired: true);
        await service.UpsertMappingAsync("req-source", "state",   "status",     isRequired: false);

        // Act
        IReadOnlyList<string> required = await service.GetRequiredFieldsAsync("req-source");

        // Assert — only number and company are required
        Assert.That(required, Is.EquivalentTo(new[] { "number", "company" }));
    }

    // ── GetRequiredFields ──────────────────────────────────────────────────

    [Test]
    public async Task GetRequiredFields_ReturnsOnlyRequiredFields()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        await service.CreateSourceAsync("rf-source", "RF Source");
        await service.UpsertMappingAsync("rf-source", "col_a", "canonical_a", isRequired: true);
        await service.UpsertMappingAsync("rf-source", "col_b", "canonical_b", isRequired: false);
        await service.UpsertMappingAsync("rf-source", "col_c", "canonical_c", isRequired: true);

        // Act
        IReadOnlyList<string> required = await service.GetRequiredFieldsAsync("rf-source");

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
        string? canonical = await service.GetCanonicalNameAsync("no-source", "no-field");

        // Assert
        Assert.That(canonical, Is.Null);
    }

    [Test]
    public async Task GetCanonicalName_ReturnsCanonicalName_WhenMappingExists()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        await service.CreateSourceAsync("cn-source", "CN Source");
        await service.UpsertMappingAsync("cn-source", "short_description", "title", isRequired: false);

        // Act
        string? canonical = await service.GetCanonicalNameAsync("cn-source", "short_description");

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
            await service.GetSourceDefinitionAsync("ghost-source"));
    }

    [Test]
    public async Task GetSourceDefinition_ReturnsMappings()
    {
        // Arrange
        IItsmSourceService service = BuildIsolatedService();
        await service.CreateSourceAsync("def-source", "Def Source");
        await service.UpsertMappingAsync("def-source", "number",  "ticket_key", isRequired: true);
        await service.UpsertMappingAsync("def-source", "company", "company",    isRequired: true);
        await service.UpsertMappingAsync("def-source", "state",   "status",     isRequired: false);

        // Act
        ItsmSourceDefinition definition = await service.GetSourceDefinitionAsync("def-source");

        // Assert
        Assert.That(definition.Source.Name, Is.EqualTo("def-source"));
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
        await service.CreateSourceAsync("dm-source", "DM Source");
        await service.UpsertMappingAsync("dm-source", "field_a", "canonical_a", isRequired: false);
        await service.UpsertMappingAsync("dm-source", "field_b", "canonical_b", isRequired: false);
        await service.UpsertMappingAsync("dm-source", "field_c", "canonical_c", isRequired: false);

        // Act
        await service.DeleteMappingAsync("dm-source", "field_b");

        // Assert — field_b gone, field_a and field_c remain
        string? aCanonical = await service.GetCanonicalNameAsync("dm-source", "field_a");
        string? bCanonical = await service.GetCanonicalNameAsync("dm-source", "field_b");
        string? cCanonical = await service.GetCanonicalNameAsync("dm-source", "field_c");

        Assert.That(aCanonical, Is.EqualTo("canonical_a"));
        Assert.That(bCanonical, Is.Null);
        Assert.That(cCanonical, Is.EqualTo("canonical_c"));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an <see cref="IItsmSourceService"/> backed by a fresh isolated SQLite in-memory
    /// database so each test has a clean slate independent of the shared fixture database.
    /// </summary>
    private static IItsmSourceService BuildIsolatedService()
    {
        var dbName = $"itsm_source_test_{System.Guid.NewGuid():N}";
        var connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        // Open a connection to keep the in-memory DB alive for the test
        var keepAlive = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        keepAlive.Open();

        using var cmd = keepAlive.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS itsm_source (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                name          TEXT NOT NULL,
                display_label TEXT NOT NULL,
                created_at    DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT uq_itsm_source_name UNIQUE (name)
            );

            CREATE TABLE IF NOT EXISTS itsm_field_mapping (
                itsm_source          TEXT NOT NULL,
                source_field_name    TEXT NOT NULL,
                canonical_field_name TEXT NOT NULL,
                is_required          INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (itsm_source, source_field_name)
            );
            """;
        cmd.ExecuteNonQuery();

        Func<System.Data.IDbConnection> factory = () =>
            new Microsoft.Data.Sqlite.SqliteConnection(connectionString);

        return new ItsmSourceService(factory);
    }
}
