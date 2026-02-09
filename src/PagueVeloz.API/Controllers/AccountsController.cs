using MediatR;
using Microsoft.AspNetCore.Mvc;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.UseCases.Accounts;
using Swashbuckle.AspNetCore.Annotations;

namespace PagueVeloz.API.Controllers;

/// <summary>
/// Controller for managing accounts.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AccountsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Creates a new account.
    /// </summary>
    /// <remarks>
    /// Creates a new account with the specified initial balance and credit limit.
    /// </remarks>
    /// <param name="request">The account creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created account details.</returns>
    [HttpPost]
    [SwaggerOperation(Summary = "Creates a new account", Description = "Creates a new account with the specified initial balance and credit limit.")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateAccountCommand(
            request.ClientId,
            request.AccountId,
            request.InitialBalance,
            request.CreditLimit,
            request.Currency);

        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetBalance), new { accountId = result.AccountId }, result);
    }

    /// <summary>
    /// Retrieves the balance of a specific account.
    /// </summary>
    /// <remarks>
    /// Returns the current, reserved, and available balance for the specified account ID.
    /// </remarks>
    /// <param name="accountId">The unique identifier of the account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The account balance details.</returns>
    [HttpGet("{accountId}/balance")]
    [SwaggerOperation(Summary = "Retrieves account balance", Description = "Returns the current, reserved, and available balance for the specified account ID.")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(string accountId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetBalanceQuery(accountId), cancellationToken);
        return Ok(result);
    }
}
