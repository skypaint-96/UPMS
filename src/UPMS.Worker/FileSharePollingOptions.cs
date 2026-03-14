namespace UPMS.Worker;

public sealed class FileSharePollingOptions
{
    public bool Enabled { get; set; }
    public int PollIntervalSeconds { get; set; } = 60;
    public string? WatchedPath { get; set; }
    public string[] FilePatterns { get; set; } = ["*.csv", "*.json"];
    public string? ArchivePath { get; set; }
    public string? ErrorPath { get; set; }
    public int? MaxFilesPerCycle { get; set; }
    public string? ItsmSource { get; set; }
    public int StableFileAgeSeconds { get; set; } = 30;
}
