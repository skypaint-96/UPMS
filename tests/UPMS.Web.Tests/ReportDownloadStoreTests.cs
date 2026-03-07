namespace UPMS.Web.Tests;

using UPMS.Web;
using UPMS.Web.Plugins;

[TestFixture]
public class ReportDownloadStoreTests
{
    [Test]
    public void Store_WhenReportHasFileContent_ReturnsToken()
    {
        ReportDownloadStore store = new();
        ReportResult result = CreateFileResult();

        string token = store.Store(result);

        Assert.That(token, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void TryGet_WhenTokenExists_ReturnsDownloadAndKeepsEntryUntilExpiry()
    {
        ReportDownloadStore store = new();
        ReportResult result = CreateFileResult();
        string token = store.Store(result);

        bool found = store.TryGet(token, out PendingReportDownload? download);
        bool foundAgain = store.TryGet(token, out PendingReportDownload? secondAttempt);

        Assert.That(found, Is.True);
        Assert.That(download, Is.Not.Null);
        Assert.That(download!.FileName, Is.EqualTo("report.csv"));
        Assert.That(download.ContentType, Is.EqualTo("text/csv"));
        Assert.That(download.FileContent, Is.EqualTo(result.FileContent));
        Assert.That(foundAgain, Is.True);
        Assert.That(secondAttempt, Is.Not.Null);
        Assert.That(secondAttempt!.FileName, Is.EqualTo("report.csv"));
    }

    [Test]
    public void TryGet_WhenTokenIsUnknown_ReturnsFalse()
    {
        ReportDownloadStore store = new();

        bool found = store.TryGet("missing-token", out PendingReportDownload? download);

        Assert.That(found, Is.False);
        Assert.That(download, Is.Null);
    }

    [Test]
    public void Store_WhenReportHasNoFileContent_Throws()
    {
        ReportDownloadStore store = new();
        ReportResult result = new()
        {
            Success = true,
            OutputType = ReportOutputType.FileDownload,
            FileName = "empty.csv",
            ContentType = "text/csv",
            FileContent = null
        };

        Assert.That(() => store.Store(result), Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void TryGet_WhenEntryExpires_ReturnsFalse()
    {
        ReportDownloadStore store = new();
        ReportResult result = CreateFileResult();
        string token = store.Store(result, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(15);

        bool found = store.TryGet(token, out PendingReportDownload? download);

        Assert.That(found, Is.False);
        Assert.That(download, Is.Null);
    }

    private static ReportResult CreateFileResult() => new()
    {
        Success = true,
        OutputType = ReportOutputType.FileDownload,
        FileName = "report.csv",
        ContentType = "text/csv",
        FileContent = "a,b\n1,2\n"u8.ToArray()
    };
}
