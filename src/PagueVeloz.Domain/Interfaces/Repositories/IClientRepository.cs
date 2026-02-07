using PagueVeloz.Domain.Entities;

namespace PagueVeloz.Domain.Interfaces.Repositories;

public interface IClientRepository
{
    Task<Client?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    ValueTask AddAsync(Client client, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string clientId, CancellationToken cancellationToken = default);
}
