using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PagueVeloz.Infrastructure.Persistence.Context;
using Swashbuckle.AspNetCore.Annotations;

namespace PagueVeloz.API.Controllers;

/// <summary>
/// Controller for database maintenance operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class DatabaseController(
    ApplicationDbContext dbContext,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Purges the database (clears all data).
    /// </summary>
    /// <remarks>
    /// Truncates the transactions, accounts, and clients tables. This operation is only allowed if 'EnableDatabasePurge' configuration is true.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the purge operation.</returns>
    [HttpPost("purge")]
    [SwaggerOperation(Summary = "Purges the database", Description = "Truncates the transactions, accounts, and clients tables. Requires 'EnableDatabasePurge' to be enabled.")]
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
