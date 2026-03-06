namespace UPMS.Web.Plugins;

/// <summary>
/// Output model returned by a plugin's GenerateAsync method.
/// </summary>
public class ReportResult
{
    public required bool Success { get; init; }
    public required ReportOutputType OutputType { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }
    public byte[]? FileContent { get; init; }
    public string? HtmlContent { get; init; }
    public string? ErrorMessage { get; init; }

    public static ReportResult Failure(string errorMessage) => new()
    {
        Success = false,
        OutputType = ReportOutputType.PlainText,
        ErrorMessage = errorMessage
    };
}
