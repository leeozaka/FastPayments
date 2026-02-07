using MassTransit;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Domain.Interfaces.Repositories;

namespace PagueVeloz.Infrastructure.Sagas.Consumers;

public sealed class CompensateDebitConsumer(
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    ILogger<CompensateDebitConsumer> logger) : IConsumer<CompensateDebitCommand>
{
    public async Task Consume(ConsumeContext<CompensateDebitCommand> context)
    {
        var message = context.Message;
        logger.LogInformation(
            "Saga {CorrelationId}: Compensating debit — crediting {Amount} back to account {AccountId}",
            message.CorrelationId, message.Amount, message.AccountId);

        try
        {
            var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var account = await accountRepository.GetByAccountIdAsync(message.AccountId, ct)
                    ?? throw new InvalidOperationException($"Source account '{message.AccountId}' not found during compensation.");

                var transaction = account.Credit(message.Amount, message.Currency, message.ReferenceId);

                await accountRepository.UpdateAsync(account, ct);
                await transactionRepository.AddAsync(transaction, ct);

                return new
                {
                    account.Balance,
                    account.ReservedBalance,
                    account.AvailableBalance
                };
            }, context.CancellationToken);

            await context.Publish(new CompensateDebitCompleted
            {
                CorrelationId = message.CorrelationId,
                SourceBalance = result.Balance,
                SourceReservedBalance = result.ReservedBalance,
                SourceAvailableBalance = result.AvailableBalance
            });

            logger.LogInformation(
                "Saga {CorrelationId}: Compensation completed. Balance restored to {Balance}",
                message.CorrelationId, result.Balance);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex,
                "Saga {CorrelationId}: COMPENSATION FAILED for account {AccountId}. Manual intervention required!",
                message.CorrelationId, message.AccountId);

            throw;
        }
    }
}
