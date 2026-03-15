namespace UPMS.Worker;

public sealed record FileSharePollingCycleResult(
    string Outcome,
    int DiscoveredCount,
    int QueuedCount,
    int QuarantinedCount,
    int SkippedCount,
    string? Message)
{
    public bool IsFailure => Outcome is "invalid-configuration" or "path-unavailable";

    public static FileSharePollingCycleResult Empty { get; } = new("empty", 0, 0, 0, 0, null);

    public static FileSharePollingCycleResult Disabled(string? message = null)
        => new("disabled", 0, 0, 0, 0, message);

    public static FileSharePollingCycleResult InvalidConfiguration(string message)
        => new("invalid-configuration", 0, 0, 0, 0, message);

    public static FileSharePollingCycleResult PathUnavailable(string message)
        => new("path-unavailable", 0, 0, 0, 0, message);

    public static FileSharePollingCycleResult Completed(
        int discoveredCount,
        int queuedCount,
        int quarantinedCount,
        int skippedCount,
        string? message = null)
        => new("completed", discoveredCount, queuedCount, quarantinedCount, skippedCount, message);
}
