namespace UPMS.Web.Templates;

public static class ReportTemplateContentTypeMapper
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".html"] = "text/html",
            [".htm"] = "text/html",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".xml"] = "application/xml",
            [".eml"] = "message/rfc822",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation"
        };

    public static IReadOnlyCollection<string> SupportedExtensions => ContentTypes.Keys.ToList().AsReadOnly();

    public static bool IsSupported(string extension) => ContentTypes.ContainsKey(Normalize(extension));

    public static string GetContentType(string extension)
    {
        var normalized = Normalize(extension);
        return ContentTypes.TryGetValue(normalized, out var contentType)
            ? contentType
            : "application/octet-stream";
    }

    public static bool IsTextLike(string extension)
    {
        var normalized = Normalize(extension);
        return normalized is ".html" or ".htm" or ".txt" or ".csv" or ".xml" or ".eml";
    }

    public static bool IsOoxmlPackage(string extension)
    {
        var normalized = Normalize(extension);
        return normalized is ".docx" or ".xlsx" or ".pptx";
    }

    public static string Normalize(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return string.Empty;

        return extension.StartsWith(".", StringComparison.Ordinal)
            ? extension.ToLowerInvariant()
            : $".{extension.ToLowerInvariant()}";
    }
}
