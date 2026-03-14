namespace UPMS.Worker;

public sealed record FileSharePollingCycleResult(
    int DiscoveredCount,
    int QueuedCount,
    int QuarantinedCount,
    int SkippedCount)
{
    public static FileSharePollingCycleResult Empty { get; } = new(0, 0, 0, 0);
    public static FileSharePollingCycleResult Disabled { get; } = Empty;
    public static FileSharePollingCycleResult InvalidConfiguration { get; } = Empty;
}
