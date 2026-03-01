namespace UPMS.Data.Tests;

using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using UPMS.Data;

/// <summary>
/// Unit tests for <see cref="EfCommandRepository"/> using the EF Core in-memory provider.
/// Each test gets a fresh isolated <see cref="UpmsDbContext"/> so there is no shared state.
/// </summary>
/// <remarks>
/// <see cref="EfCommandRepository.AddSnapshotTicketsAsync"/> uses <c>ExecuteSqlRawAsync</c>
/// which is not supported by the in-memory provider and is therefore excluded from this suite.
/// It is covered by integration tests that run against a real Postgres database.
/// </remarks>
[TestFixture]
public class EfCommandRepositoryTests
{
    private UpmsDbContext _context = null!;
    private EfCommandRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<UpmsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new UpmsDbContext(options);
        _repository = new EfCommandRepository(_context);
    }

    [TearDown]
    public void TearDown() => _context.Dispose();

    // ── CreateSnapshotAsync ────────────────────────────────────────────────

    [Test]
    public async Task CreateSnapshotAsync_PersistsSnapshot()
    {
        // arrange
        var snapshot = new Snapshot
        {
            Id           = Guid.NewGuid(),
            ItsmSource   = "test-source",
            SnapshotDate = DateTime.UtcNow,
            UploadedBy   = "test-user",
            UploadedAt   = DateTime.UtcNow,
        };

        // act
        var result = await _repository.CreateSnapshotAsync(snapshot);

        // assert
        Assert.That(result.Id, Is.EqualTo(snapshot.Id));
        var stored = await _context.Snapshots.FindAsync(snapshot.Id);
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.ItsmSource, Is.EqualTo("test-source"));
    }

    [Test]
    public async Task CreateSnapshotAsync_ReturnsTheSameEntity()
    {
        // arrange
        var snapshot = new Snapshot
        {
            Id           = Guid.NewGuid(),
            ItsmSource   = "source-a",
            SnapshotDate = DateTime.UtcNow,
            UploadedBy   = "user-a",
            UploadedAt   = DateTime.UtcNow,
        };

        // act
        var returned = await _repository.CreateSnapshotAsync(snapshot);

        // assert — the returned object is the same reference with the same Id
        Assert.That(returned, Is.SameAs(snapshot));
        Assert.That(returned.Id, Is.EqualTo(snapshot.Id));
    }

    [Test]
    public void CreateSnapshotAsync_ThrowsOnNull()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _repository.CreateSnapshotAsync(null!));
    }

    // ── RecordFieldChangeAsync ─────────────────────────────────────────────

    [Test]
    public async Task RecordFieldChangeAsync_PersistsFieldChange()
    {
        // arrange — need a snapshot first (FK constraint even on in-memory)
        var snapshotId = Guid.NewGuid();
        _context.Snapshots.Add(new Snapshot
        {
            Id           = snapshotId,
            ItsmSource   = "src",
            SnapshotDate = DateTime.UtcNow,
            UploadedBy   = "u",
            UploadedAt   = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        var fieldChange = new FieldChange
        {
            CompanyName = "ACME",
            TicketKey   = "INC-001",
            FieldName   = "Status",
            FieldValue  = "Open",
            ObservedAt  = DateTime.UtcNow,
            SnapshotId  = snapshotId,
        };

        // act
        await _repository.RecordFieldChangeAsync(fieldChange);

        // assert
        var stored = _context.FieldChanges.Single();
        Assert.That(stored.TicketKey,   Is.EqualTo("INC-001"));
        Assert.That(stored.FieldName,   Is.EqualTo("Status"));
        Assert.That(stored.CompanyName, Is.EqualTo("ACME"));
        Assert.That(stored.FieldValue,  Is.EqualTo("Open"));
    }

    [Test]
    public void RecordFieldChangeAsync_ThrowsOnNull()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _repository.RecordFieldChangeAsync(null!));
    }

    // ── RecordFieldChangesAsync ────────────────────────────────────────────

    [Test]
    public async Task RecordFieldChangesAsync_PersistsMultipleChanges()
    {
        // arrange
        var snapshotId = Guid.NewGuid();
        _context.Snapshots.Add(new Snapshot
        {
            Id           = snapshotId,
            ItsmSource   = "src",
            SnapshotDate = DateTime.UtcNow,
            UploadedBy   = "u",
            UploadedAt   = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        var changes = new[]
        {
            new FieldChange { CompanyName = "ACME", TicketKey = "INC-001", FieldName = "Status",   FieldValue = "Open",  ObservedAt = DateTime.UtcNow, SnapshotId = snapshotId },
            new FieldChange { CompanyName = "ACME", TicketKey = "INC-001", FieldName = "Priority", FieldValue = "High",  ObservedAt = DateTime.UtcNow, SnapshotId = snapshotId },
        };

        // act
        await _repository.RecordFieldChangesAsync(changes);

        // assert
        Assert.That(_context.FieldChanges.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task RecordFieldChangesAsync_EmptyCollection_DoesNothing()
    {
        // act — should not throw
        await _repository.RecordFieldChangesAsync(Array.Empty<FieldChange>());

        // assert — nothing was added
        Assert.That(_context.FieldChanges.Count(), Is.EqualTo(0));
    }

    [Test]
    public void RecordFieldChangesAsync_ThrowsOnNull()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _repository.RecordFieldChangesAsync(null!));
    }

    // ── AddSnapshotTicketAsync ─────────────────────────────────────────────

    [Test]
    [Ignore("AddSnapshotTicketsAsync uses ExecuteSqlRawAsync which requires a real Postgres database.")]
    public async Task AddSnapshotTicketsAsync_PersistsTickets()
    {
        var snapshotId = Guid.NewGuid();
        _context.Snapshots.Add(new Snapshot
        {
            Id           = snapshotId,
            ItsmSource   = "src",
            SnapshotDate = DateTime.UtcNow,
            UploadedBy   = "u",
            UploadedAt   = DateTime.UtcNow,
        });
        await _context.SaveChangesAsync();

        var tickets = new[]
        {
            new SnapshotTicket { Id = Guid.NewGuid(), SnapshotId = snapshotId, CompanyName = "ACME", TicketKey = "INC-001" },
            new SnapshotTicket { Id = Guid.NewGuid(), SnapshotId = snapshotId, CompanyName = "ACME", TicketKey = "INC-002" },
        };

        await _repository.AddSnapshotTicketsAsync(tickets);

        Assert.That(_context.SnapshotTickets.Count(), Is.EqualTo(2));
    }
}
