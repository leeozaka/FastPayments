using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PagueVeloz.Infrastructure.Persistence.Context;

namespace PagueVeloz.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DatabaseController(
    ApplicationDbContext dbContext,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("purge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Purge(CancellationToken cancellationToken)
    {
        var enabled = configuration.GetValue<bool>("EnableDatabasePurge");
        if (!enabled)
            return Forbid();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE transactions, accounts, clients CASCADE
            """,
            cancellationToken);

        return Ok(new { message = "Database purged successfully" });
    }
}
