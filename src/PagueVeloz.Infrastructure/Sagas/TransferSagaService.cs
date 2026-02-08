using MassTransit;
using PagueVeloz.Application.Sagas.Transfer;

namespace PagueVeloz.Infrastructure.Sagas;

public sealed class TransferSagaService(
    IBus bus
    ) : ITransferSagaService
{
    private static readonly TimeSpan SagaTimeout = TimeSpan.FromSeconds(30);

    public async Task<TransferSagaResult> ExecuteTransferAsync(
        string sourceAccountId,
        string destinationAccountId,
        long amount,
        string currency,
        string referenceId,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(SagaTimeout);

        var completedTask = new TaskCompletionSource<TransferSagaResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        var completedHandle = bus.ConnectReceiveEndpoint(
            $"transfer-response-{correlationId:N}",
            cfg =>
            {
                cfg.Handler<TransferCompleted>(ctx =>
                {
                    if (ctx.Message.CorrelationId != correlationId) return Task.CompletedTask;

                    completedTask.TrySetResult(new TransferSagaResult
                    {
                        Success = true,
                        SagaId = ctx.Message.CorrelationId,
                        DebitTransactionId = ctx.Message.DebitTransactionId,
                        CreditTransactionId = ctx.Message.CreditTransactionId,
                        SourceBalance = ctx.Message.SourceBalance,
                        SourceReservedBalance = ctx.Message.SourceReservedBalance,
                        SourceAvailableBalance = ctx.Message.SourceAvailableBalance
                    });
                    return Task.CompletedTask;
                });

                cfg.Handler<TransferFailed>(ctx =>
                {
                    if (ctx.Message.CorrelationId != correlationId) return Task.CompletedTask;

                    completedTask.TrySetResult(new TransferSagaResult
                    {
                        Success = false,
                        SagaId = ctx.Message.CorrelationId,
                        FailureReason = ctx.Message.Reason
                    });
                    return Task.CompletedTask;
                });
            });

        try
        {
            await completedHandle.Ready;

            await bus.Publish(new TransferRequested
            {
                CorrelationId = correlationId,
                SourceAccountId = sourceAccountId,
                DestinationAccountId = destinationAccountId,
                Amount = amount,
                Currency = currency,
                ReferenceId = referenceId,
                Metadata = metadata
            }, cancellationToken);

            await using (cts.Token.Register(() =>
                completedTask.TrySetCanceled(cts.Token)))
            {
                return await completedTask.Task;
            }
        }
        finally
        {
            await completedHandle.StopAsync(cancellationToken);
        }
    }
}
