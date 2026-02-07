namespace PagueVeloz.Application.Interfaces;

public interface IDistributedLockService
{
    Task<IAsyncDisposable> AcquireLockAsync(string resource, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
}
