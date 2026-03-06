namespace UPMS.Web.Tests;

using Microsoft.Extensions.Options;
using UPMS.Data;
using UPMS.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Unit / integration tests for <see cref="SnapshotIngestService"/>.
/// All write-path calls go through <see cref="FakeCommandRepository"/> — no database required.
/// </summary>
[TestFixture]
public class IngestServiceTests
{
    // ── IngestResult factory ───────────────────────────────────────────────

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

    // ── Flat-table CSV ingest ──────────────────────────────────────────────

    [Test]
    [NonParallelizable]
    public async Task IngestCsv_ValidFlatTable_StoresAllTickets()
    {
        // Arrange — source with mappings for number→ticket_key, company→company, short_description→title
        var (service, _) = BuildServiceWithMappings(new[]
        {
            ("number",            "ticket_key", true),
            ("company",           "company",    true),
            ("short_description", "title",      false),
            ("priority",          "priority",   false),
            ("state",             "status",     false),
        });

        const string csv = """
            number,company,short_description,priority,state
            INC001,Acme Corp,Cannot login,High,In Progress
            INC002,Acme Corp,Email broken,Medium,New
            INC003,Globex Ltd,VPN down,High,Open
            """;

        using var stream = MakeStream(csv);

        // Act
        IngestResult result = await service.IngestCsvAsync(
            stream, "test-source", new DateOnly(2025, 1, 15));

        // Assert
        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.TicketsIngested, Is.EqualTo(3));
        // company is stored as metadata (snapshot_ticket/company_name) and is not stored as a ticket field.
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(12)); // 3 tickets × 4 stored fields (ticket_number + 3 other columns)
    }

    // ... rest of file unchanged ...

    private static System.IO.MemoryStream MakeStream(string text) =>
        new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));

    /// <summary>
    /// Builds a <see cref="SnapshotIngestService"/> backed by a <see cref="FakeCommandRepository"/>.
    /// The supplied mappings are seeded into the fake source service.
    /// </summary>
    private static (SnapshotIngestService Service, FakeCommandRepository Repository) BuildServiceWithMappings(
        IEnumerable<(string SourceField, string Canonical, bool IsRequired)> mappings)
    {
        var fakeCommandRepository = new FakeCommandRepository();

        var fakeSourceService = new FakeItsmSourceService();
        foreach (var (src, canonical, required) in mappings)
        {
            fakeSourceService.AddMapping("test-source", src, canonical, required);
        }

        var service = new SnapshotIngestService(fakeCommandRepository, fakeSourceService, NullLogger<SnapshotIngestService>.Instance);
        return (service, fakeCommandRepository);
    }

    // ── Fake ICommandRepository ────────────────────────────────────────────

    private class FakeCommandRepository : ICommandRepository
    {
        public List<Snapshot> CreatedSnapshots { get; } = new();
        public List<SnapshotTicket> AddedTickets { get; } = new();
        public List<FieldChange> RecordedChanges { get; } = new();

        public Task<Snapshot> CreateSnapshotAsync(Snapshot snapshot, CancellationToken ct = default)
        {
            CreatedSnapshots.Add(snapshot);
            return Task.FromResult(snapshot);
        }

        public Task AddSnapshotTicketAsync(SnapshotTicket ticket, CancellationToken ct = default)
        {
            AddedTickets.Add(ticket);
            return Task.CompletedTask;
        }

        public Task AddSnapshotTicketsAsync(IEnumerable<SnapshotTicket> tickets, CancellationToken ct = default)
        {
            AddedTickets.AddRange(tickets);
            return Task.CompletedTask;
        }

        public Task RecordFieldChangeAsync(FieldChange fieldChange, CancellationToken ct = default)
        {
            RecordedChanges.Add(fieldChange);
            return Task.CompletedTask;
        }

        public Task RecordFieldChangesAsync(IEnumerable<FieldChange> changes, CancellationToken ct = default)
        {
            RecordedChanges.AddRange(changes);
            return Task.CompletedTask;
        }
    }

    // ── Fake IItsmSourceService ────────────────────────────────────────────

    private class FakeItsmSourceService : IItsmSourceService
    {
        // sourceName → (sourceField → (canonical, isRequired))
        private readonly Dictionary<string, Dictionary<string, (string Canonical, bool IsRequired)>> _mappings
            = new(StringComparer.Ordinal);

        public void AddMapping(string sourceName, string sourceField, string canonical, bool isRequired)
        {
            if (!_mappings.TryGetValue(sourceName, out var dict))
            {
                dict = new Dictionary<string, (string, bool)>(StringComparer.Ordinal);
                _mappings[sourceName] = dict;
            }
            dict[sourceField] = (canonical, isRequired);
        }

        public Task<IReadOnlyList<ItsmSource>> GetAllSourcesAsync() =>
            Task.FromResult<IReadOnlyList<ItsmSource>>(Array.Empty<ItsmSource>());

        public Task<ItsmSource?> GetSourceByNameAsync(string name) =>
            Task.FromResult<ItsmSource?>(null);

        public Task<ItsmSourceDefinition> GetSourceDefinitionAsync(string name) =>
            throw new KeyNotFoundException(name);

        public Task<ItsmSource> CreateSourceAsync(string name, string displayLabel) =>
            throw new NotImplementedException();

        public Task DeleteSourceAsync(string name) => Task.CompletedTask;

        public Task UpsertMappingAsync(string sourceName, string sourceFieldName, string canonicalName, bool isRequired)
        {
            AddMapping(sourceName, sourceFieldName, canonicalName, isRequired);
            return Task.CompletedTask;
        }

        public Task DeleteMappingAsync(string sourceName, string sourceFieldName) => Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetRequiredFieldsAsync(string sourceName)
        {
            if (!_mappings.TryGetValue(sourceName, out var dict))
                return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

            var required = dict
                .Where(kv => kv.Value.IsRequired)
                .Select(kv => kv.Key)
                .ToList()
                .AsReadOnly();
            return Task.FromResult<IReadOnlyList<string>>(required);
        }

        public Task<string?> GetCanonicalNameAsync(string sourceName, string sourceFieldName)
        {
            if (_mappings.TryGetValue(sourceName, out var dict) &&
                dict.TryGetValue(sourceFieldName, out var entry))
            {
                return Task.FromResult<string?>(entry.Canonical);
            }
            return Task.FromResult<string?>(null);
        }
    }
}
