namespace UPMS.Web.Tests;

using Moq;
using NUnit.Framework;
using UPMS.Data;
using UPMS.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Unit tests for <see cref="SnapshotIngestService"/> that verify the write-path
/// correctly calls <see cref="ICommandRepository"/> methods during ingestion.
/// All dependencies are mocked — no database required.
/// </summary>
[TestFixture]
public class SnapshotIngestServiceCommandTests
{
    private Mock<ICommandRepository> _commandRepoMock = null!;
    private Mock<IItsmSourceService> _sourceServiceMock = null!;
    private SnapshotIngestService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _commandRepoMock = new Mock<ICommandRepository>(MockBehavior.Strict);
        _sourceServiceMock = new Mock<IItsmSourceService>(MockBehavior.Strict);
        _service = new SnapshotIngestService(_commandRepoMock.Object, _sourceServiceMock.Object, NullLogger<SnapshotIngestService>.Instance);
    }

    // ── Happy-path CSV ingest ──────────────────────────────────────────────

    /// <summary>
    /// Verifies that a valid CSV ingest calls CreateSnapshotAsync,
    /// AddSnapshotTicketsAsync, and RecordFieldChangesAsync with correct ticket count.
    /// </summary>
    [Test]
    public async Task IngestCsvAsync_HappyPath_CallsAllCommandRepositoryMethods()
    {
        // arrange
        const string sourceName = "test-source";
        var snapshotDate = new DateOnly(2025, 6, 1);

        // source service: number→ticket_key (required), company→company (required), state→status
        _sourceServiceMock
            .Setup(s => s.GetRequiredFieldsAsync(sourceName))
            .ReturnsAsync(new List<string> { "number", "company" }.AsReadOnly());

        _sourceServiceMock
            .Setup(s => s.GetCanonicalNameAsync(sourceName, "number"))
            .ReturnsAsync("ticket_key");
        _sourceServiceMock
            .Setup(s => s.GetCanonicalNameAsync(sourceName, "company"))
            .ReturnsAsync("company");
        _sourceServiceMock
            .Setup(s => s.GetCanonicalNameAsync(sourceName, "state"))
            .ReturnsAsync("status");

        // command repo: capture what is passed in
        Snapshot? capturedSnapshot = null;
        _commandRepoMock
            .Setup(r => r.CreateSnapshotAsync(It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .Callback<Snapshot, CancellationToken>((s, _) => capturedSnapshot = s)
            .ReturnsAsync((Snapshot s, CancellationToken _) => s);

        _commandRepoMock
            .Setup(r => r.AddSnapshotTicketsAsync(It.IsAny<IEnumerable<SnapshotTicket>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _commandRepoMock
            .Setup(r => r.RecordFieldChangesAsync(It.IsAny<IEnumerable<FieldChange>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        const string csv = """
            number,company,state
            INC001,Acme Corp,Open
            INC002,Globex Ltd,New
            """;

        using var stream = MakeStream(csv);

        // act
        IngestResult result = await _service.IngestCsvAsync(stream, sourceName, snapshotDate);

        // assert — result
        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.TicketsIngested, Is.EqualTo(2));
        // company is stored as metadata (snapshot_ticket/company_name) and is not stored as a ticket field.
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(4)); // 2 tickets × 2 stored fields (ticket_number + state)

        // assert — snapshot created with correct source name
        Assert.That(capturedSnapshot, Is.Not.Null);
        Assert.That(capturedSnapshot!.ItsmSource, Is.EqualTo(sourceName));

        // assert — all three ICommandRepository methods were called exactly once
        _commandRepoMock.Verify(r => r.CreateSnapshotAsync(
            It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()), Times.Once);
        _commandRepoMock.Verify(r => r.AddSnapshotTicketsAsync(
            It.IsAny<IEnumerable<SnapshotTicket>>(), It.IsAny<CancellationToken>()), Times.Once);
        _commandRepoMock.Verify(r => r.RecordFieldChangesAsync(
            It.IsAny<IEnumerable<FieldChange>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MemoryStream MakeStream(string text) =>
        new(System.Text.Encoding.UTF8.GetBytes(text));
}
