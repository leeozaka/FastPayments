using MediatR;
using Microsoft.AspNetCore.Mvc;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.UseCases.Transactions;

namespace PagueVeloz.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TransactionsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ProcessTransaction(
        [FromBody] TransactionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ProcessTransactionCommand(
            request.Operation,
            request.AccountId,
            request.Amount,
            request.Currency,
            request.ReferenceId,
            request.DestinationAccountId,
            request.Metadata);

        var result = await mediator.Send(command, cancellationToken);

        return result.Status == "failed"
            ? UnprocessableEntity(result)
            : Ok(result);
    }

    [HttpPost("batch")]
    [ProducesResponseType(typeof(List<TransactionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessBatch(
        [FromBody] List<TransactionRequest> requests,
        CancellationToken cancellationToken)
    {
        var results = new List<TransactionResponse>();

        foreach (var request in requests)
        {
            var command = new ProcessTransactionCommand(
                request.Operation,
                request.AccountId,
                request.Amount,
                request.Currency,
                request.ReferenceId,
                request.DestinationAccountId,
                request.Metadata);

            var result = await mediator.Send(command, cancellationToken);
            results.Add(result);
        }

        return Ok(results);
    }
}
