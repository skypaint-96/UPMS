namespace UPMS.Api.Controllers;

/// <summary>
/// Basic health check response.
/// </summary>
public class HealthResponse
{
    /// <summary>
    /// Health status (healthy/unhealthy).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp of the health check.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
