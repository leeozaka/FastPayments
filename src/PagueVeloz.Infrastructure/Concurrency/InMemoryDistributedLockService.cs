using System.Collections.Concurrent;
using PagueVeloz.Application.Interfaces;

namespace PagueVeloz.Infrastructure.Concurrency;

public sealed class InMemoryDistributedLockService : IDistributedLockService, IDisposable
{
    private static readonly ConcurrentDictionary<string, LockEntry> Locks = new();
    private static readonly TimeSpan EvictionInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaxIdleTime = TimeSpan.FromMinutes(5);
    private readonly Timer _evictionTimer;

    public InMemoryDistributedLockService()
    {
        _evictionTimer = new Timer(EvictStaleEntries, null, EvictionInterval, EvictionInterval);
    }

    public async Task<IAsyncDisposable> AcquireLockAsync(
        string resource,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var entry = Locks.GetOrAdd(resource, _ => new LockEntry());
        entry.LastUsed = Environment.TickCount64;

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);

        var acquired = await entry.Semaphore.WaitAsync(effectiveTimeout, cancellationToken).ConfigureAwait(false);

        if (!acquired)
            throw new TimeoutException($"Could not acquire lock for resource '{resource}' within {effectiveTimeout.TotalSeconds}s.");

        return new LockHandle(entry);
    }

    private static void EvictStaleEntries(object? state)
    {
        var now = Environment.TickCount64;
        foreach (var kvp in Locks)
        {
            var entry = kvp.Value;
            if (now - entry.LastUsed > MaxIdleTime.TotalMilliseconds
                && entry.Semaphore.CurrentCount == 1) // Not currently held
            {
                if (Locks.TryRemove(kvp.Key, out var removed))
                    removed.Semaphore.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _evictionTimer.Dispose();
    }

    private sealed class LockEntry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public long LastUsed { get; set; } = Environment.TickCount64;
    }

    private sealed class LockHandle(LockEntry entry) : IAsyncDisposable
    {
        private bool _disposed;

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                entry.LastUsed = Environment.TickCount64;
                entry.Semaphore.Release();
                _disposed = true;
            }

            return ValueTask.CompletedTask;
        }
    }
}
