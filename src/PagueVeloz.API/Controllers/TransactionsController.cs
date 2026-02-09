using MediatR;
using Microsoft.AspNetCore.Mvc;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.UseCases.Transactions;
using Swashbuckle.AspNetCore.Annotations;

namespace PagueVeloz.API.Controllers;

/// <summary>
/// Controller for processing transactions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class TransactionsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Processes a single transaction.
    /// </summary>
    /// <remarks>
    /// Processes a transaction (credit, debit, transfer) based on the provided request.
    /// </remarks>
    /// <param name="request">The transaction request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The transaction execution result.</returns>
    [HttpPost]
    [SwaggerOperation(Summary = "Processes a transaction", Description = "Processes a stored or immediate transaction based on the operation type.")]
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

    /// <summary>
    /// Processes a batch of transactions.
    /// </summary>
    /// <remarks>
    /// Processes multiple transactions in a single request.
    /// </remarks>
    /// <param name="requests">The list of transaction requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of transaction execution results.</returns>
    [HttpPost("batch")]
    [SwaggerOperation(Summary = "Processes a batch of transactions", Description = "Submit multiple transactions at once.")]
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
