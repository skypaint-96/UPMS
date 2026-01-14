namespace UPMS.Api.Controllers;

/// <summary>
/// Readiness check response with detailed component checks.
/// </summary>
public class ReadinessResponse
{
    /// <summary>
    /// Overall readiness status (ready/unhealthy).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the readiness check.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Individual component check results.
    /// </summary>
    public Dictionary<string, CheckResult> Checks { get; set; } = new();
}
