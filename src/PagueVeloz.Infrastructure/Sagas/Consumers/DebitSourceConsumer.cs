using MassTransit;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Domain.Enums;
using PagueVeloz.Domain.Exceptions;
using PagueVeloz.Domain.Interfaces.Repositories;
using Polly;
using Polly.Registry;

namespace PagueVeloz.Infrastructure.Sagas.Consumers;

public sealed class DebitSourceConsumer(
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<DebitSourceConsumer> logger) : IConsumer<DebitSourceCommand>
{
    public async Task Consume(ConsumeContext<DebitSourceCommand> context)
    {
        var message = context.Message;
        logger.LogInformation(
            "Saga {CorrelationId}: Debiting {Amount} from account {AccountId}",
            message.CorrelationId, message.Amount, message.AccountId);

        var pipeline = pipelineProvider.GetPipeline("database");

        try
        {
            var result = await pipeline.ExecuteAsync(async ct =>
            {
                return await unitOfWork.ExecuteInTransactionAsync(async ct2 =>
                {
                    var account = await accountRepository.GetByAccountIdAsync(message.AccountId, ct2)
                        ?? throw new InvalidOperationException($"Source account '{message.AccountId}' not found.");

                    var transaction = account.Debit(message.Amount, message.Currency, message.ReferenceId, message.Metadata);

                    if (transaction.Status == TransactionStatus.Failed)
                        throw new DomainException("DEBIT_FAILED", transaction.ErrorMessage ?? "Debit failed");

                    await accountRepository.UpdateAsync(account, ct2);
                    await transactionRepository.AddAsync(transaction, ct2);

                    return new
                    {
                        transaction.ReferenceId,
                        account.Balance,
                        account.ReservedBalance,
                        account.AvailableBalance
                    };
                }, ct).ConfigureAwait(false);
            }, context.CancellationToken).ConfigureAwait(false);

            await context.Publish(new DebitSourceCompleted
            {
                CorrelationId = message.CorrelationId,
                TransactionId = result.ReferenceId,
                SourceBalance = result.Balance,
                SourceReservedBalance = result.ReservedBalance,
                SourceAvailableBalance = result.AvailableBalance
            });

            logger.LogInformation(
                "Saga {CorrelationId}: Debit completed. New balance: {Balance}",
                message.CorrelationId, result.Balance);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Saga {CorrelationId}: Debit step failed for account {AccountId}",
                message.CorrelationId, message.AccountId);

            await context.Publish(new DebitSourceFailed
            {
                CorrelationId = message.CorrelationId,
                Reason = ex.Message
            });
        }
    }
}
