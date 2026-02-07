using PagueVeloz.Domain.Entities;

namespace PagueVeloz.Domain.Interfaces.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByAccountIdAsync(string accountId, CancellationToken cancellationToken = default);
    Task<Account?> GetByAccountIdWithLockAsync(string accountId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    ValueTask AddAsync(Account account, CancellationToken cancellationToken = default);
    ValueTask UpdateAsync(Account account, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default);
}
