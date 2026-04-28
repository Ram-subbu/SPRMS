using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SPRMS.Common;

namespace SPRMS.Controllers;

/// <summary>
/// Health and status endpoints.
/// Provides application health status and diagnostics information.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[AllowAnonymous]
public class HealthController : BaseController
{
    /// <summary>
    /// Get application health status.
    /// </summary>
    /// <returns>Health status information</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        var status = new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        };
        return Ok(status);
    }

    /// <summary>
    /// Simple status check for monitoring.
    /// </summary>
    /// <returns>Status OK or error</returns>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        return Ok(new { status = "running", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Check API readiness.
    /// </summary>
    /// <returns>Ready status</returns>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetReady()
    {
        return Ok(new { ready = true, timestamp = DateTime.UtcNow });
    }
}
