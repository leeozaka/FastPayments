using Microsoft.EntityFrameworkCore;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Interfaces.Repositories;
using PagueVeloz.Infrastructure.Persistence.Context;

namespace PagueVeloz.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository(IReadDbContext readContext, ApplicationDbContext writeContext) : ITransactionRepository
{
    public async Task<Transaction?> GetByReferenceIdAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        return await readContext.Transactions
            .FirstOrDefaultAsync(t => t.ReferenceId == referenceId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return await readContext.Transactions
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.Timestamp)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await writeContext.Transactions
            .AddAsync(transaction, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsByReferenceIdAsync(string referenceId, CancellationToken cancellationToken = default)
    {
        return await readContext.Transactions
            .AnyAsync(t => t.ReferenceId == referenceId, cancellationToken)
            .ConfigureAwait(false);
    }
}
