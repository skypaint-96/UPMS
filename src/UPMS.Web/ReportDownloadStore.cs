namespace UPMS.Web;

using UPMS.Web.Plugins;

/// <summary>
/// Stores the last generated file report result temporarily for download.
/// Registered as a scoped service so it's per-connection.
/// </summary>
public class ReportDownloadStore
{
    public ReportResult? PendingDownload { get; set; }
}
