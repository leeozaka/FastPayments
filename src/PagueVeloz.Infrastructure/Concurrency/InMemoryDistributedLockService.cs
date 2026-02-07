using System.Collections.Concurrent;
using PagueVeloz.Application.Interfaces;

namespace PagueVeloz.Infrastructure.Concurrency;

public sealed class InMemoryDistributedLockService : IDistributedLockService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public async Task<IAsyncDisposable> AcquireLockAsync(
        string resource,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var semaphore = Locks.GetOrAdd(resource, _ => new SemaphoreSlim(1, 1));
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);

        var acquired = await semaphore.WaitAsync(effectiveTimeout, cancellationToken).ConfigureAwait(false);

        if (!acquired)
            throw new TimeoutException($"Could not acquire lock for resource '{resource}' within {effectiveTimeout.TotalSeconds}s.");

        return new LockHandle(semaphore);
    }

    private sealed class LockHandle(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        private bool _disposed;

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                semaphore.Release();
                _disposed = true;
            }

            return ValueTask.CompletedTask;
        }
    }
}
