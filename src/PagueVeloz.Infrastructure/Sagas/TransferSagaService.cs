using System.Collections.Concurrent;
using MassTransit;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Sagas.Transfer;
using PagueVeloz.Infrastructure.Mappers;

namespace PagueVeloz.Infrastructure.Sagas;

public sealed class TransferSagaService : ITransferSagaService, IDisposable
{
    private static readonly TimeSpan SagaTimeout = TimeSpan.FromSeconds(30);

    private readonly IBus _bus;
    private readonly ILogger<TransferSagaService> _logger;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<TransferSagaResult>> _pendingTransfers = new();
    private HostReceiveEndpointHandle? _endpointHandle;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public TransferSagaService(IBus bus, ILogger<TransferSagaService> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    private async Task EnsureEndpointAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            _endpointHandle = _bus.ConnectReceiveEndpoint(
                "transfer-responses-shared",
                cfg =>
                {
                    cfg.Handler<TransferCompleted>(ctx =>
                    {
                        if (_pendingTransfers.TryGetValue(ctx.Message.CorrelationId, out var tcs))
                            tcs.TrySetResult(ctx.Message.ToResult());
                        return Task.CompletedTask;
                    });

                    cfg.Handler<TransferFailed>(ctx =>
                    {
                        if (_pendingTransfers.TryGetValue(ctx.Message.CorrelationId, out var tcs))
                            tcs.TrySetResult(ctx.Message.ToResult());
                        return Task.CompletedTask;
                    });
                });

            await _endpointHandle.Ready.ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<TransferSagaResult> ExecuteTransferAsync(
        string sourceAccountId,
        string destinationAccountId,
        long amount,
        string currency,
        string referenceId,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureEndpointAsync(cancellationToken).ConfigureAwait(false);

        var correlationId = Guid.NewGuid();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(SagaTimeout);

        var completedTask = new TaskCompletionSource<TransferSagaResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingTransfers.TryAdd(correlationId, completedTask);

        try
        {
            await _bus.Publish(new TransferRequested
            {
                CorrelationId = correlationId,
                SourceAccountId = sourceAccountId,
                DestinationAccountId = destinationAccountId,
                Amount = amount,
                Currency = currency,
                ReferenceId = referenceId,
                Metadata = metadata
            }, cancellationToken).ConfigureAwait(false);

            await using (cts.Token.Register(() =>
                completedTask.TrySetCanceled(cts.Token)))
            {
                return await completedTask.Task.ConfigureAwait(false);
            }
        }
        finally
        {
            _pendingTransfers.TryRemove(correlationId, out _);
        }
    }

    public void Dispose()
    {
        _endpointHandle?.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        _initLock.Dispose();
    }
}
