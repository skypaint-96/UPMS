namespace UPMS.Data.Jobs;

public static class BackgroundJobTypes
{
    public const string SnapshotIngest = "snapshot-ingest";
    public const string ReportExecution = "report-execution";
    public const string ReportDelivery = "report-delivery";
    public const string FileSharePoll = "file-share-poll";
}
