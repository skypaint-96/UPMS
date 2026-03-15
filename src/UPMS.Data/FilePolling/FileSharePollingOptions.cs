namespace UPMS.Data;

public sealed class FileSharePollingOptions
{
    public bool Enabled { get; set; }
    public bool AllowUserManagedSources { get; set; } = true;
    public int SchedulerIntervalSeconds { get; set; } = 30;
    public int DefaultPollIntervalSeconds { get; set; } = 300;
    public int MinPollIntervalSeconds { get; set; } = 60;
    public int MaxPollIntervalSeconds { get; set; } = 86400;
    public int DefaultStableFileAgeSeconds { get; set; } = 30;
    public int MaxFilesPerCycleCap { get; set; } = 100;
    public string[] AllowedWatchedRoots { get; set; } = [];
    public string[] AllowedArchiveRoots { get; set; } = [];
    public string[] AllowedErrorRoots { get; set; } = [];
}
