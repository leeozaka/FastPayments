using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Interfaces;

namespace PagueVeloz.Infrastructure.Storage;

public sealed class InMemorySagaStateRepository<TSaga>(ILogger<InMemorySagaStateRepository<TSaga>> logger) : ISagaStateRepository<TSaga> where TSaga : class
{
    private readonly ConcurrentDictionary<Guid, string> _store = new();

    public Task<TSaga?> LoadAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_store.TryGetValue(correlationId, out var json))
        {
            try
            {
                var saga = JsonSerializer.Deserialize<TSaga>(json);
                logger.LogDebug("Saga state loaded for CorrelationId: {CorrelationId}", correlationId);
                return Task.FromResult(saga);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize saga state for CorrelationId: {CorrelationId}", correlationId);
                return Task.FromResult<TSaga?>(null);
            }
        }

        logger.LogDebug("Saga state not found for CorrelationId: {CorrelationId}", correlationId);
        return Task.FromResult<TSaga?>(null);
    }

    public Task SaveAsync(Guid correlationId, TSaga saga, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var json = JsonSerializer.Serialize(saga);
        _store[correlationId] = json;

        logger.LogDebug("Saga state saved for CorrelationId: {CorrelationId}", correlationId);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _store.TryRemove(correlationId, out _);
        logger.LogDebug("Saga state deleted for CorrelationId: {CorrelationId}", correlationId);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.ContainsKey(correlationId));
    }
}
