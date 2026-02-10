using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Mappers;
using PagueVeloz.Application.UseCases.Transactions;
using Swashbuckle.AspNetCore.Annotations;

namespace PagueVeloz.API.Controllers;

/// <summary>
/// Controller for processing transactions.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[TranslateResultToActionResult]
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
    [ExpectedFailures(ResultStatus.Invalid, ResultStatus.Error)]
    public async Task<Result<TransactionResponse>> ProcessTransaction(
        [FromBody] TransactionRequest request,
        CancellationToken cancellationToken)
    {
        var command = request.ToCommand();
        return await mediator.Send(command, cancellationToken);
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
        var indexedRequests = requests.Select((r, i) => (Request: r, Index: i)).ToList();
        var results = new TransactionResponse[requests.Count];

        var accountGroups = indexedRequests.GroupBy(x => x.Request.AccountId);

        await Parallel.ForEachAsync(
            accountGroups,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = cancellationToken
            },
            async (group, ct) =>
            {
                foreach (var (request, index) in group)
                {
                    var command = request.ToCommand();
                    var result = await mediator.Send(command, ct);

                    results[index] = result.IsSuccess
                        ? result.Value
                        : new TransactionResponse
                        {
                            TransactionId = request.ReferenceId,
                            Status = "failed",
                            ErrorMessage = string.Join("; ", result.Errors),
                            Timestamp = DateTime.UtcNow
                        };
                }
            });

        return Ok(results.ToList());
    }
}
