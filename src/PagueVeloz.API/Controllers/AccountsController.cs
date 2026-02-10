using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Mappers;
using PagueVeloz.Application.UseCases.Accounts;
using Swashbuckle.AspNetCore.Annotations;

namespace PagueVeloz.API.Controllers;

/// <summary>
/// Controller for managing accounts.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[TranslateResultToActionResult]
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
    [ExpectedFailures(ResultStatus.Invalid)]
    public async Task<Result<AccountResponse>> Create([FromBody] CreateAccountRequest request, CancellationToken cancellationToken)
    {
        var command = request.ToCommand();
        return await mediator.Send(command, cancellationToken);
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
    [ExpectedFailures(ResultStatus.NotFound)]
    public async Task<Result<AccountResponse>> GetBalance(string accountId, CancellationToken cancellationToken)
    {
        return await mediator.Send(new GetBalanceQuery(accountId), cancellationToken);
    }
}
