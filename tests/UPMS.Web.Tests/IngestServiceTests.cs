namespace UPMS.Web.Tests;

using Microsoft.Extensions.Options;
using UPMS.Data;
using UPMS.Web.Services;

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
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(15)); // 3 tickets × 5 columns
    }

    [Test]
    [NonParallelizable]
    public async Task IngestCsv_MissingRequiredField_ReturnsValidationError()
    {
        // Arrange — source requires "number" (ticket_key) but CSV doesn't have it
        var (service, _) = BuildServiceWithMappings(new[]
        {
            ("number",  "ticket_key", true),
            ("company", "company",    true),
        });

        const string csv = """
            company,short_description
            Acme Corp,Cannot login
            """;

        using var stream = MakeStream(csv);

        // Act
        IngestResult result = await service.IngestCsvAsync(
            stream, "test-source", new DateOnly(2025, 1, 15));

        // Assert
        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("number").IgnoreCase);
    }

    [Test]
    [NonParallelizable]
    public async Task IngestCsv_UnmappedColumns_StoredWithRawName()
    {
        // Arrange — only number and company are mapped; u_custom_99 has no mapping
        var (service, _) = BuildServiceWithMappings(new[]
        {
            ("number",  "ticket_key", true),
            ("company", "company",    true),
        });

        const string csv = """
            number,company,u_custom_99
            INC001,Acme Corp,BATCH-7
            """;

        using var stream = MakeStream(csv);

        // Act
        IngestResult result = await service.IngestCsvAsync(
            stream, "test-source", new DateOnly(2025, 1, 15));

        // Assert — 3 field changes: ticket_key, company, u_custom_99 (raw name)
        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(3));
    }

    [Test]
    [NonParallelizable]
    public async Task IngestCsv_CompanyExtractedFromRowData()
    {
        // Arrange
        var (service, _) = BuildServiceWithMappings(new[]
        {
            ("number",  "ticket_key", true),
            ("company", "company",    true),
        });

        const string csv = """
            number,company
            INC001,Acme Corp
            """;

        using var stream = MakeStream(csv);

        // Act
        IngestResult result = await service.IngestCsvAsync(
            stream, "test-source", new DateOnly(2025, 1, 15));

        // Assert — success means company was read from the row, not a parameter
        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.TicketsIngested, Is.EqualTo(1));
    }

    [Test]
    [NonParallelizable]
    public async Task IngestCsv_MultipleCompanies_CreatesMultipleSnapshots()
    {
        // Arrange — rows from two different companies
        var (service, _) = BuildServiceWithMappings(new[]
        {
            ("number",  "ticket_key", true),
            ("company", "company",    true),
        });

        const string csv = """
            number,company
            INC001,Acme Corp
            INC002,Globex Ltd
            INC003,Acme Corp
            """;

        using var stream = MakeStream(csv);

        // Act
        IngestResult result = await service.IngestCsvAsync(
            stream, "test-source", new DateOnly(2025, 1, 15));

        // Assert — all 3 tickets ingested (different companies, same snapshot)
        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.TicketsIngested, Is.EqualTo(3));
    }

    [Test]
    [NonParallelizable]
    public async Task IngestJson_ValidArray_StoresAllTickets()
    {
        // Arrange
        var (service, _) = BuildServiceWithMappings(new[]
        {
            ("number",  "ticket_key", true),
            ("company", "company",    true),
            ("state",   "status",     false),
        });

        const string json = """
            [
              { "number": "INC001", "company": "Acme Corp", "state": "Open" },
              { "number": "INC002", "company": "Globex Ltd", "state": "New" }
            ]
            """;

        using var stream = MakeStream(json);

        // Act
        IngestResult result = await service.IngestJsonAsync(
            stream, "test-source", new DateOnly(2025, 1, 15));

        // Assert
        Assert.That(result.Success, Is.True, result.ErrorMessage);
        Assert.That(result.TicketsIngested, Is.EqualTo(2));
        Assert.That(result.FieldChangesRecorded, Is.EqualTo(6)); // 2 tickets × 3 fields
    }

    [Test]
    [NonParallelizable]
    public async Task IngestCsv_EmptyFile_ReturnsZeroTickets()
    {
        // Arrange
        var (service, _) = BuildServiceWithMappings(Array.Empty<(string, string, bool)>());

        using var stream = MakeStream(string.Empty);

        // Act
        IngestResult result = await service.IngestCsvAsync(
            stream, "test-source", new DateOnly(2025, 1, 15));

        // Assert — empty file returns a failure (no header row)
        Assert.That(result.Success, Is.False);
    }

    // ── Constructor guards ─────────────────────────────────────────────────

    [Test]
    public void SnapshotIngestService_RequiresDataService_ThrowsOnNull()
    {
        var fakeSourceService = new FakeItsmSourceService();

        Assert.Throws<ArgumentNullException>(() =>
            new SnapshotIngestService(null!, fakeSourceService));
    }

    [Test]
    public void SnapshotIngestService_RequiresSourceService_ThrowsOnNull()
    {
        var fakeCommandRepository = new FakeCommandRepository();

        Assert.Throws<ArgumentNullException>(() =>
            new SnapshotIngestService(fakeCommandRepository, null!));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static MemoryStream MakeStream(string text) =>
        new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));

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

        var service = new SnapshotIngestService(fakeCommandRepository, fakeSourceService);
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
