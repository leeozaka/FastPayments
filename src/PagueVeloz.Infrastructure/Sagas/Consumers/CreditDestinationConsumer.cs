using MassTransit;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Domain.Interfaces.Repositories;

namespace PagueVeloz.Infrastructure.Sagas.Consumers;

public sealed class CreditDestinationConsumer(
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    ILogger<CreditDestinationConsumer> logger) : IConsumer<CreditDestinationCommand>
{
    public async Task Consume(ConsumeContext<CreditDestinationCommand> context)
    {
        var message = context.Message;
        logger.LogInformation(
            "Saga {CorrelationId}: Crediting {Amount} to account {AccountId}",
            message.CorrelationId, message.Amount, message.AccountId);

        try
        {
            var transactionId = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var account = await accountRepository.GetByAccountIdAsync(message.AccountId, ct)
                    ?? throw new InvalidOperationException($"Destination account '{message.AccountId}' not found.");

                var transaction = account.Credit(message.Amount, message.Currency, message.ReferenceId, message.Metadata);

                await accountRepository.UpdateAsync(account, ct);
                await transactionRepository.AddAsync(transaction, ct);

                return transaction.ReferenceId;
            }, context.CancellationToken);

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
