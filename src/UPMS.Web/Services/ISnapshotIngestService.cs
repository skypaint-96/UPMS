namespace UPMS.Web.Services;

/// <summary>
/// Parses uploaded snapshot files and persists the extracted ticket data.
/// </summary>
public interface ISnapshotIngestService
{
    /// <summary>
    /// Parses a CSV snapshot file stream and records all tickets and field changes.
    /// Returns an IngestResult summarising what was processed.
    /// </summary>
    Task<IngestResult> IngestCsvAsync(
        Stream fileStream,
        string itsmSource,
        DateTime snapshotDate,
        string uploadedBy,
        string companyName,
        CancellationToken ct = default);

    /// <summary>
    /// Parses a JSON snapshot file stream and records all tickets and field changes.
    /// </summary>
    Task<IngestResult> IngestJsonAsync(
        Stream fileStream,
        string itsmSource,
        DateTime snapshotDate,
        string uploadedBy,
        string companyName,
        CancellationToken ct = default);
}
