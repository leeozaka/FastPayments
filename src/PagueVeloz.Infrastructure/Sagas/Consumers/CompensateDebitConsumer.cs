using MassTransit;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Domain.Interfaces.Repositories;
using Polly.Registry;
using SerilogContext = Serilog.Context.LogContext;

namespace PagueVeloz.Infrastructure.Sagas.Consumers;

public sealed class CompensateDebitConsumer(
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<CompensateDebitConsumer> logger) : IConsumer<CompensateDebitCommand>
{
    public async Task Consume(ConsumeContext<CompensateDebitCommand> context)
    {
        var message = context.Message;

        using (SerilogContext.PushProperty("ReferenceId", message.ReferenceId))
        using (SerilogContext.PushProperty("AccountId", message.AccountId))
        {
            logger.LogInformation("Saga: Compensating debit of {Amount}", message.Amount);

            var pipeline = pipelineProvider.GetPipeline("database");

            try
            {
                var result = await pipeline.ExecuteAsync(async ct =>
                {
                    return await unitOfWork.ExecuteInTransactionAsync(async ct2 =>
                    {
                        var account = await accountRepository.GetByAccountIdAsync(message.AccountId, ct2)
                            ?? throw new InvalidOperationException($"Source account '{message.AccountId}' not found during compensation.");

                        var transaction = account.Credit(message.Amount, message.Currency, message.ReferenceId);

                        await accountRepository.UpdateAsync(account, ct2);
                        await transactionRepository.AddAsync(transaction, ct2);

                        return new
                        {
                            account.Balance,
                            account.ReservedBalance,
                            account.AvailableBalance
                        };
                    }, ct).ConfigureAwait(false);
                }, context.CancellationToken).ConfigureAwait(false);

                await context.Publish(new CompensateDebitCompleted
                {
                    CorrelationId = message.CorrelationId,
                    SourceBalance = result.Balance,
                    SourceReservedBalance = result.ReservedBalance,
                    SourceAvailableBalance = result.AvailableBalance
                });

                logger.LogInformation("Saga: Compensation completed. New balance: {Balance}", result.Balance);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Saga: Compensation failed");
                throw; // Let MassTransit handle retry
            }
        }
    }
}
