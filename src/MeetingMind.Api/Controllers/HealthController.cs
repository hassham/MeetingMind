using MeetingMind.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MeetingMind.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "Healthy",
            service = "MeetingMind.Api",
            timestamp = DateTime.UtcNow
        });
    }

    [HttpGet("/health/db")]
    public async Task<IActionResult> GetDatabaseHealth(
        IOperationalReadinessService readinessService,
        CancellationToken cancellationToken)
    {
        var checks = await readinessService.CheckAsync(cancellationToken);
        var database = checks.Single(check => check.Name == "database");

        return database.IsHealthy
            ? Ok(new { status = "Healthy", database = "PostgreSQL" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Unhealthy",
                database = "PostgreSQL"
            });
    }

    [HttpGet("/health/ready")]
    public async Task<IActionResult> GetReadiness(
        IOperationalReadinessService readinessService,
        CancellationToken cancellationToken)
    {
        var checks = await readinessService.CheckAsync(cancellationToken);
        var isHealthy = checks.All(check => check.IsHealthy);
        var response = new
        {
            status = isHealthy ? "Healthy" : "Unhealthy",
            checks = checks.Select(check => new
            {
                name = check.Name,
                status = check.IsHealthy ? "Healthy" : "Unhealthy"
            })
        };

        return isHealthy
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
