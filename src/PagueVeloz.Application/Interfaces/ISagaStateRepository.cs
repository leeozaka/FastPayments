namespace PagueVeloz.Application.Interfaces;

public interface ISagaStateRepository<TSaga> where TSaga : class
{
    Task<TSaga?> LoadAsync(Guid correlationId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid correlationId, TSaga saga, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid correlationId, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
