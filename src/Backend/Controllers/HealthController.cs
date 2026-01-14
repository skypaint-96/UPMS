using Microsoft.AspNetCore.Mvc;
using UPMS.Api.Data;

namespace UPMS.Api.Controllers;

/// <summary>
/// Health check endpoints for monitoring and container orchestration.
/// </summary>
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IDbConnectionFactory connectionFactory, ILogger<HealthController> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>
    /// Liveness probe - returns 200 if the application is running.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new HealthResponse
        {
            Status = "healthy",
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Readiness probe - returns 200 if the application can serve requests.
    /// Checks database connectivity.
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        try
        {
            using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            
            return Ok(new ReadinessResponse
            {
                Status = "ready",
                Timestamp = DateTimeOffset.UtcNow,
                Checks = new Dictionary<string, CheckResult>
                {
                    ["database"] = new CheckResult { Status = "healthy", Message = "Connected" }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            
            return StatusCode(503, new ReadinessResponse
            {
                Status = "unhealthy",
                Timestamp = DateTimeOffset.UtcNow,
                Checks = new Dictionary<string, CheckResult>
                {
                    ["database"] = new CheckResult { Status = "unhealthy", Message = ex.Message }
                }
            });
        }
    }
}
