namespace UPMS.Data.Artifacts;

public interface IArtifactStorage
{
    Task<StoredArtifact> SaveAsync(
        string category,
        string fileName,
        Stream content,
        string? contentType,
        CancellationToken ct = default);

    Task<StoredArtifact> SaveBytesAsync(
        string category,
        string fileName,
        byte[] content,
        string? contentType,
        CancellationToken ct = default);

    bool Exists(string relativePath);
    string GetAbsolutePath(string relativePath);
    Stream OpenRead(string relativePath);
    void Delete(string relativePath);
}
