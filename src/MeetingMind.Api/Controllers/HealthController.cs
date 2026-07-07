using MeetingMind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        MeetingMindDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? Ok(new { status = "Healthy", database = "PostgreSQL" })
            : Problem("Database connection failed");
    }
}
