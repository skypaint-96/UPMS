namespace UPMS.Data.Artifacts;

public sealed record StoredArtifact(
    string RelativePath,
    string AbsolutePath,
    string FileName,
    string? ContentType,
    long ContentLength,
    string? Sha256 = null);
