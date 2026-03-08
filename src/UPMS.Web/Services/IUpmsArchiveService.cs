namespace UPMS.Web.Services;

public interface IUpmsArchiveService
{
    Task<ArchiveDownload> ExportSnapshotAsync(Guid snapshotId, CancellationToken ct = default);
    Task<ArchiveDownload> ExportSourceAsync(string sourceName, CancellationToken ct = default);
    Task DeleteSnapshotAsync(Guid snapshotId, CancellationToken ct = default);
    Task DeleteSourceAsync(string sourceName, CancellationToken ct = default);
    Task<SourceImportResult> ImportSourceAsync(Stream archiveStream, bool replaceExisting, CancellationToken ct = default);
}

public sealed record ArchiveDownload(byte[] Content, string ContentType, string FileName);

public sealed record SourceImportResult(
    string SourceName,
    int MappingCount,
    int SnapshotCount,
    int FieldChangeCount);
