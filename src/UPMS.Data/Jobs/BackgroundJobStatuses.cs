namespace UPMS.Data.Jobs;

public static class BackgroundJobStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";

    public static bool IsTerminal(string status) =>
        string.Equals(status, Succeeded, StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, Failed, StringComparison.OrdinalIgnoreCase);
}
