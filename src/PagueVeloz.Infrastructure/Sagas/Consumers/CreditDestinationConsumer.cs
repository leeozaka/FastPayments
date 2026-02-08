using MassTransit;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Domain.Interfaces.Repositories;
using Polly;
using Polly.Registry;

namespace PagueVeloz.Infrastructure.Sagas.Consumers;

public sealed class CreditDestinationConsumer(
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<CreditDestinationConsumer> logger) : IConsumer<CreditDestinationCommand>
{
    public async Task Consume(ConsumeContext<CreditDestinationCommand> context)
    {
        var message = context.Message;
        logger.LogInformation(
            "Saga {CorrelationId}: Crediting {Amount} to account {AccountId}",
            message.CorrelationId, message.Amount, message.AccountId);

        var pipeline = pipelineProvider.GetPipeline("database");

        try
        {
            var transactionId = await pipeline.ExecuteAsync(async ct =>
            {
                return await unitOfWork.ExecuteInTransactionAsync(async ct2 =>
                {
                    var account = await accountRepository.GetByAccountIdAsync(message.AccountId, ct2)
                        ?? throw new InvalidOperationException($"Destination account '{message.AccountId}' not found.");

                    var transaction = account.Credit(message.Amount, message.Currency, message.ReferenceId, message.Metadata);

                    await accountRepository.UpdateAsync(account, ct2);
                    await transactionRepository.AddAsync(transaction, ct2);

                    return transaction.ReferenceId;
                }, ct).ConfigureAwait(false);
            }, context.CancellationToken).ConfigureAwait(false);

            await context.Publish(new CreditDestinationCompleted
            {
                CorrelationId = message.CorrelationId,
                TransactionId = transactionId
            });

            logger.LogInformation(
                "Saga {CorrelationId}: Credit completed on account {AccountId}",
                message.CorrelationId, message.AccountId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Saga {CorrelationId}: Credit step failed for account {AccountId}",
                message.CorrelationId, message.AccountId);

            await context.Publish(new CreditDestinationFailed
            {
                CorrelationId = message.CorrelationId,
                Reason = ex.Message
            });
        }
    }
}
