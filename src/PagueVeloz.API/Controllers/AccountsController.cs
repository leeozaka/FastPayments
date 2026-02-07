using MediatR;
using Microsoft.AspNetCore.Mvc;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.UseCases.Accounts;

namespace PagueVeloz.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AccountsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
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

    [HttpGet("{accountId}/balance")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(string accountId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetBalanceQuery(accountId), cancellationToken);
        return Ok(result);
    }
}
