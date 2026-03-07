namespace UPMS.Web.Reporting;

public sealed class LifecycleMonthlyPoint
{
    public required DateTime MonthStart { get; init; }
    public required DateTime MonthEnd { get; init; }
    public required string Label { get; init; }
    public int OpenedCount { get; init; }
    public int ResolvedCount { get; init; }
    public int UpdatedCount { get; init; }
    public int BacklogAtMonthEnd { get; init; }
    public int HighPriorityBacklogAtMonthEnd { get; init; }
}
