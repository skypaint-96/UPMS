namespace UPMS.Web.Services;

using UPMS.Data;

/// <summary>
/// Stub implementation of ISnapshotIngestService.
/// Full implementation is Stage 4 of the build plan.
/// </summary>
public class SnapshotIngestService : ISnapshotIngestService
{
    private readonly TicketDataServiceInstance _dataService;
    private readonly IItsmFieldMappingService _mappingService;

    public SnapshotIngestService(
        TicketDataServiceInstance dataService,
        IItsmFieldMappingService mappingService)
    {
        _dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
        _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
    }

    public Task<IngestResult> IngestCsvAsync(
        Stream fileStream,
        string itsmSource,
        DateTime snapshotDate,
        string uploadedBy,
        string companyName,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("CSV ingest is not yet implemented. See Stage 4 in Build Stages.md.");
    }

    public Task<IngestResult> IngestJsonAsync(
        Stream fileStream,
        string itsmSource,
        DateTime snapshotDate,
        string uploadedBy,
        string companyName,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("JSON ingest is not yet implemented. See Stage 4 in Build Stages.md.");
    }
}
