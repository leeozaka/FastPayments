using PagueVeloz.Domain.Entities;

namespace PagueVeloz.Domain.Interfaces.Repositories;

public interface ITransactionRepository
{
    Task<Transaction?> GetByReferenceIdAsync(string referenceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(string accountId, CancellationToken cancellationToken = default);
    ValueTask AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<bool> ExistsByReferenceIdAsync(string referenceId, CancellationToken cancellationToken = default);
}
