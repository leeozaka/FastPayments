namespace PagueVeloz.Application.Interfaces;

public interface IKeyValueStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, T>> GetByPrefixAsync<T>(string prefix, CancellationToken cancellationToken = default);
    Task<long> IncrementAsync(string key, long delta = 1, CancellationToken cancellationToken = default);
}
