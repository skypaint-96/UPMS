namespace UPMS.Data;

using System.Text.Json;

public sealed class FileSharePollingSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string WatchedPath { get; set; } = string.Empty;
    public string FilePatternsJson { get; set; } = "[]";
    public string ArchivePath { get; set; } = string.Empty;
    public string ErrorPath { get; set; } = string.Empty;
    public string ItsmSource { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 300;
    public int? MaxFilesPerCycle { get; set; }
    public int StableFileAgeSeconds { get; set; } = 30;
    public DateTime? LastRunStartedAt { get; set; }
    public DateTime? LastRunCompletedAt { get; set; }
    public DateTime? LastSucceededAt { get; set; }
    public DateTime? NextPollDueAt { get; set; }
    public string? LastError { get; set; }
    public Guid? CurrentJobId { get; set; }
    public Guid? LastJobId { get; set; }
    public bool IsSystemManaged { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public IReadOnlyList<string> GetFilePatterns()
    {
        if (string.IsNullOrWhiteSpace(FilePatternsJson))
            return ["*.csv", "*.json"];

        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(FilePatternsJson, JsonOptions);
            return NormalizePatterns(parsed);
        }
        catch (JsonException)
        {
            return ["*.csv", "*.json"];
        }
    }

    public void SetFilePatterns(IEnumerable<string>? patterns)
    {
        FilePatternsJson = JsonSerializer.Serialize(NormalizePatterns(patterns), JsonOptions);
    }

    public static IReadOnlyList<string> NormalizePatterns(IEnumerable<string>? patterns)
    {
        var normalized = (patterns ?? Array.Empty<string>())
            .Select(pattern => pattern?.Trim())
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0
            ? ["*.csv", "*.json"]
            : normalized;
    }
}
