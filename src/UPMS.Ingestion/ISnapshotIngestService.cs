namespace UPMS.Ingestion;

using UPMS.Data;

/// <summary>
/// Parses uploaded snapshot files and persists the extracted ticket data.
/// </summary>
public interface ISnapshotIngestService
{
    /// <summary>
    /// Parses a flat-table CSV snapshot file stream and records all tickets and field changes.
    /// Row 1 must be the header row (source column names).
    /// Company is extracted from the CSV row data via the column mapped to canonical name "company".
    /// Returns an <see cref="IngestResult"/> summarising what was processed.
    /// </summary>
    Task<IngestResult> IngestCsvAsync(
        Stream csvStream,
        string itsmSourceName,
        DateOnly snapshotDate,
        SnapshotIngestMetadata? metadata = null,
        CancellationToken ct = default);

    /// <summary>
    /// Parses a JSON snapshot array stream and records all tickets and field changes.
    /// Company is extracted from each JSON object's field mapped to canonical name "company".
    /// Returns an <see cref="IngestResult"/> summarising what was processed.
    /// </summary>
    Task<IngestResult> IngestJsonAsync(
        Stream jsonStream,
        string itsmSourceName,
        DateOnly snapshotDate,
        SnapshotIngestMetadata? metadata = null,
        CancellationToken ct = default);
}
