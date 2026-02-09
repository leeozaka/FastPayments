using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Interfaces;

namespace PagueVeloz.Infrastructure.Storage;

public sealed class InMemoryKeyValueStore : IKeyValueStore
{
    private readonly ConcurrentDictionary<string, string> _store = new();
    private readonly ILogger<InMemoryKeyValueStore> _logger;

    public InMemoryKeyValueStore(ILogger<InMemoryKeyValueStore> logger)
    {
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_store.TryGetValue(key, out var json))
        {
            try
            {
                var value = JsonSerializer.Deserialize<T>(json);
                _logger.LogDebug("Key-value store get for key: {Key}", key);
                return Task.FromResult(value);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize value for key: {Key}", key);
                return Task.FromResult<T?>(default);
            }
        }

        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var json = JsonSerializer.Serialize(value);
        _store[key] = json;

        _logger.LogDebug("Key-value store set for key: {Key}", key);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _store.TryRemove(key, out _);
        _logger.LogDebug("Key-value store removed for key: {Key}", key);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.ContainsKey(key));
    }

    public Task<IReadOnlyDictionary<string, T>> GetByPrefixAsync<T>(string prefix, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = new Dictionary<string, T>();

        foreach (var kvp in _store)
        {
            if (kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
            {
                try
                {
                    var value = JsonSerializer.Deserialize<T>(kvp.Value);
                    if (value is not null)
                    {
                        result[kvp.Key] = value;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize value for key: {Key}", kvp.Key);
                }
            }
        }

        _logger.LogDebug("Key-value store prefix query for: {Prefix}, Found: {Count}", prefix, result.Count);
        return Task.FromResult<IReadOnlyDictionary<string, T>>(result);
    }

    public Task<long> IncrementAsync(string key, long delta = 1, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (true)
        {
            if (_store.TryGetValue(key, out var currentJson))
            {
                var current = JsonSerializer.Deserialize<long>(currentJson);
                var newValue = current + delta;
                var newJson = JsonSerializer.Serialize(newValue);

                if (_store.TryUpdate(key, newJson, currentJson))
                {
                    _logger.LogDebug("Key-value store increment for key: {Key}, Delta: {Delta}, NewValue: {NewValue}", key, delta, newValue);
                    return Task.FromResult(newValue);
                }
            }
            else
            {
                var initialJson = JsonSerializer.Serialize(delta);
                if (_store.TryAdd(key, initialJson))
                {
                    _logger.LogDebug("Key-value store increment (new key) for key: {Key}, Value: {Value}", key, delta);
                    return Task.FromResult(delta);
                }
            }
        }
    }
}
