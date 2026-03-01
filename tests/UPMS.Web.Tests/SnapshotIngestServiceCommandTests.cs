namespace UPMS.Web.Tests;

using Moq;
using NUnit.Framework;
using UPMS.Data;
using UPMS.Web.Services;

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
        _service = new SnapshotIngestService(_commandRepoMock.Object, _sourceServiceMock.Object);
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
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(6)); // 2 tickets × 3 columns

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

    /// <summary>
    /// Verifies that a valid JSON array ingest calls CreateSnapshotAsync,
    /// AddSnapshotTicketsAsync, and RecordFieldChangesAsync with correct ticket count.
    /// </summary>
    [Test]
    public async Task IngestJsonAsync_HappyPath_CallsAllCommandRepositoryMethods()
    {
        // arrange
        const string sourceName = "json-source";
        var snapshotDate = new DateOnly(2025, 6, 1);

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
            .Setup(s => s.GetCanonicalNameAsync(sourceName, "priority"))
            .ReturnsAsync("priority");

        _commandRepoMock
            .Setup(r => r.CreateSnapshotAsync(It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Snapshot s, CancellationToken _) => s);

        _commandRepoMock
            .Setup(r => r.AddSnapshotTicketsAsync(It.IsAny<IEnumerable<SnapshotTicket>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _commandRepoMock
            .Setup(r => r.RecordFieldChangesAsync(It.IsAny<IEnumerable<FieldChange>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        const string json = """
            [
              { "number": "INC001", "company": "Acme Corp", "priority": "High" },
              { "number": "INC002", "company": "Globex Ltd", "priority": "Medium" },
              { "number": "INC003", "company": "Initech",   "priority": "Low" }
            ]
            """;

        using var stream = MakeStream(json);

        // act
        IngestResult result = await _service.IngestJsonAsync(stream, sourceName, snapshotDate);

        // assert
        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.TicketsIngested, Is.EqualTo(3));
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(9)); // 3 tickets × 3 fields

        _commandRepoMock.Verify(r => r.CreateSnapshotAsync(
            It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()), Times.Once);
        _commandRepoMock.Verify(r => r.AddSnapshotTicketsAsync(
            It.IsAny<IEnumerable<SnapshotTicket>>(), It.IsAny<CancellationToken>()), Times.Once);
        _commandRepoMock.Verify(r => r.RecordFieldChangesAsync(
            It.IsAny<IEnumerable<FieldChange>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Failure paths that must NOT call ICommandRepository ───────────────

    /// <summary>
    /// When the source has a required field that is missing from the CSV header,
    /// the service must return a failure and never touch ICommandRepository.
    /// </summary>
    [Test]
    public async Task IngestCsvAsync_MissingRequiredField_DoesNotCallCommandRepository()
    {
        // arrange
        const string sourceName = "strict-source";

        // "number" is required but the CSV omits it
        _sourceServiceMock
            .Setup(s => s.GetRequiredFieldsAsync(sourceName))
            .ReturnsAsync(new List<string> { "number", "company" }.AsReadOnly());

        // canonical lookups for the columns that ARE present
        _sourceServiceMock
            .Setup(s => s.GetCanonicalNameAsync(sourceName, It.IsAny<string>()))
            .ReturnsAsync((string _, string field) => field == "company" ? "company" : null);

        const string csv = """
            company,state
            Acme Corp,Open
            """;

        using var stream = MakeStream(csv);

        // act
        IngestResult result = await _service.IngestCsvAsync(stream, sourceName, snapshotDate: new DateOnly(2025, 6, 1));

        // assert — failure returned
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("number").IgnoreCase);

        // assert — ICommandRepository was never touched
        _commandRepoMock.Verify(r => r.CreateSnapshotAsync(
            It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()), Times.Never);
        _commandRepoMock.Verify(r => r.AddSnapshotTicketsAsync(
            It.IsAny<IEnumerable<SnapshotTicket>>(), It.IsAny<CancellationToken>()), Times.Never);
        _commandRepoMock.Verify(r => r.RecordFieldChangesAsync(
            It.IsAny<IEnumerable<FieldChange>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// An empty CSV (no header row) must return a failure without calling ICommandRepository.
    /// </summary>
    [Test]
    public async Task IngestCsvAsync_EmptyCsv_DoesNotCallCommandRepository()
    {
        // arrange
        const string sourceName = "empty-source";

        _sourceServiceMock
            .Setup(s => s.GetRequiredFieldsAsync(sourceName))
            .ReturnsAsync(new List<string>().AsReadOnly());

        using var stream = MakeStream(string.Empty);

        // act
        IngestResult result = await _service.IngestCsvAsync(stream, sourceName, new DateOnly(2025, 1, 1));

        // assert
        Assert.That(result.Success, Is.False);

        _commandRepoMock.Verify(r => r.CreateSnapshotAsync(
            It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Snapshot field verification ───────────────────────────────────────

    /// <summary>
    /// The SnapshotDate on the persisted <see cref="Snapshot"/> must match the
    /// <paramref name="snapshotDate"/> parameter supplied to <c>IngestCsvAsync</c>.
    /// </summary>
    [Test]
    public async Task IngestCsvAsync_SnapshotDate_MatchesSuppliedDate()
    {
        // arrange
        const string sourceName = "date-source";
        var expectedDate = new DateOnly(2024, 12, 25);

        _sourceServiceMock
            .Setup(s => s.GetRequiredFieldsAsync(sourceName))
            .ReturnsAsync(new List<string> { "number", "company" }.AsReadOnly());
        _sourceServiceMock
            .Setup(s => s.GetCanonicalNameAsync(sourceName, "number"))
            .ReturnsAsync("ticket_key");
        _sourceServiceMock
            .Setup(s => s.GetCanonicalNameAsync(sourceName, "company"))
            .ReturnsAsync("company");

        DateTime? capturedDate = null;
        _commandRepoMock
            .Setup(r => r.CreateSnapshotAsync(It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .Callback<Snapshot, CancellationToken>((s, _) => capturedDate = s.SnapshotDate)
            .ReturnsAsync((Snapshot s, CancellationToken _) => s);
        _commandRepoMock
            .Setup(r => r.AddSnapshotTicketsAsync(It.IsAny<IEnumerable<SnapshotTicket>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _commandRepoMock
            .Setup(r => r.RecordFieldChangesAsync(It.IsAny<IEnumerable<FieldChange>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        const string csv = """
            number,company
            INC001,Acme Corp
            """;

        using var stream = MakeStream(csv);

        // act
        await _service.IngestCsvAsync(stream, sourceName, expectedDate);

        // assert — SnapshotDate should correspond to midnight UTC of the supplied DateOnly
        Assert.That(capturedDate, Is.Not.Null);
        Assert.That(DateOnly.FromDateTime(capturedDate!.Value), Is.EqualTo(expectedDate));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static MemoryStream MakeStream(string text) =>
        new(System.Text.Encoding.UTF8.GetBytes(text));
}
