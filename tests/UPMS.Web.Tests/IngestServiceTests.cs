namespace UPMS.Web.Tests;

using Microsoft.Extensions.Options;
using UPMS.Data;
using UPMS.Web.Services;

/// <summary>
/// Unit tests for IngestResult and SnapshotIngestService.
/// Tests that invoke the stub IngestCsvAsync/IngestJsonAsync assert NotImplementedException,
/// documenting Stage 4 pending work.
/// </summary>
[TestFixture]
public class IngestServiceTests
{
    // ── IngestResult.Failure factory ───────────────────────────────────────

    [Test]
    public void IngestResult_Failure_HasSuccessFalse()
    {
        var result = IngestResult.Failure("error");
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void IngestResult_Failure_HasEmptySnapshotId()
    {
        var result = IngestResult.Failure("error");
        Assert.That(result.SnapshotId, Is.EqualTo(Guid.Empty));
    }

    [Test]
    public void IngestResult_Failure_HasZeroTicketsIngested()
    {
        var result = IngestResult.Failure("error");
        Assert.That(result.TicketsIngested, Is.EqualTo(0));
    }

    [Test]
    public void IngestResult_Failure_HasErrorMessage()
    {
        var result = IngestResult.Failure("error message");
        Assert.That(result.ErrorMessage, Is.EqualTo("error message"));
    }

    [Test]
    public void IngestResult_Failure_HasEmptyWarnings()
    {
        var result = IngestResult.Failure("error");
        Assert.That(result.Warnings, Is.Empty);
    }

    [Test]
    public void IngestResult_Success_CanBeCreated()
    {
        // Arrange
        var snapshotId = Guid.NewGuid();

        // Act
        var result = new IngestResult
        {
            Success = true,
            SnapshotId = snapshotId,
            TicketsIngested = 5,
            FieldChangesRecorded = 20
        };

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.SnapshotId, Is.EqualTo(snapshotId));
        Assert.That(result.TicketsIngested, Is.EqualTo(5));
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(20));
    }

    // ── SnapshotIngestService stub behaviour ───────────────────────────────

    [Test]
    public void SnapshotIngestService_IngestCsvAsync_ThrowsNotImplementedException()
    {
        // Arrange — Stage 4 pending: documents that CSV ingest is not yet implemented
        var service = BuildService();
        using var stream = new MemoryStream();

        // Act & Assert
        Assert.ThrowsAsync<NotImplementedException>(() =>
            service.IngestCsvAsync(stream, "ServiceNow", DateTime.UtcNow, "tester", "Acme"));
    }

    [Test]
    public void SnapshotIngestService_IngestJsonAsync_ThrowsNotImplementedException()
    {
        // Arrange — Stage 4 pending: documents that JSON ingest is not yet implemented
        var service = BuildService();
        using var stream = new MemoryStream();

        // Act & Assert
        Assert.ThrowsAsync<NotImplementedException>(() =>
            service.IngestJsonAsync(stream, "ServiceNow", DateTime.UtcNow, "tester", "Acme"));
    }

    [Test]
    public void SnapshotIngestService_RequiresDataService_ThrowsOnNull()
    {
        var fakeMappingService = new FakeMappingService();

        Assert.Throws<ArgumentNullException>(() =>
            new SnapshotIngestService(null!, fakeMappingService));
    }

    [Test]
    public void SnapshotIngestService_RequiresMappingService_ThrowsOnNull()
    {
        var fakeOptions = Options.Create(new DatabaseOptions { ConnectionString = "Host=localhost;Database=test;" });
        var fakeDataService = new TicketDataServiceInstance(fakeOptions);

        Assert.Throws<ArgumentNullException>(() =>
            new SnapshotIngestService(fakeDataService, null!));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static SnapshotIngestService BuildService()
    {
        var fakeOptions = Options.Create(new DatabaseOptions { ConnectionString = "Host=localhost;Database=test;" });
        var fakeDataService = new TicketDataServiceInstance(fakeOptions);
        var fakeMappingService = new FakeMappingService();
        return new SnapshotIngestService(fakeDataService, fakeMappingService);
    }

    private class FakeMappingService : IItsmFieldMappingService
    {
        public string GetCanonicalName(string itsmSource, string sourceFieldName) => sourceFieldName;
        public IEnumerable<ItsmFieldMapping> GetMappingsForSource(string itsmSource) => [];
        public Task UpsertMappingAsync(string itsmSource, string sourceFieldName, string canonicalFieldName) => Task.CompletedTask;
    }
}
