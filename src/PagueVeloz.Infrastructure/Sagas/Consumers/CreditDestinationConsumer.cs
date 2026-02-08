using MassTransit;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Domain.Interfaces.Repositories;
using Polly;
using Polly.Registry;
using SerilogContext = Serilog.Context.LogContext;

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

        using (SerilogContext.PushProperty("ReferenceId", message.ReferenceId))
        using (SerilogContext.PushProperty("AccountId", message.AccountId))
        {
            logger.LogInformation("Saga: Crediting {Amount} to account", message.Amount);

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

                logger.LogInformation("Saga: Credit completed");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Saga: Credit step failed");

                await context.Publish(new CreditDestinationFailed
                {
                    CorrelationId = message.CorrelationId,
                    Reason = ex.Message
                });
            }
        }
    }
}
