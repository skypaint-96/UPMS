namespace UPMS.Api.Controllers;

/// <summary>
/// Result of an individual health check component.
/// </summary>
public class CheckResult
{
    /// <summary>
    /// Status of the check (healthy/unhealthy).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable message about the check result.
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
