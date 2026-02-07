using Microsoft.EntityFrameworkCore;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Interfaces.Repositories;
using PagueVeloz.Infrastructure.Persistence.Context;

namespace PagueVeloz.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository(IReadDbContext readContext, ApplicationDbContext writeContext) : IAccountRepository
{
    public async Task<Account?> GetByAccountIdAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return await writeContext.Accounts
            .FirstOrDefaultAsync(a => a.AccountId == accountId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Account?> GetByAccountIdWithLockAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return await writeContext.Accounts
            .FromSqlInterpolated($"SELECT * FROM accounts WHERE account_id = {accountId} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Account>> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return await readContext.Accounts
            .Where(a => a.ClientId == clientId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        await writeContext.Accounts
            .AddAsync(account, cancellationToken)
            .ConfigureAwait(false);
    }

    public ValueTask UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        writeContext.Accounts.Update(account);
        return ValueTask.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string accountId, CancellationToken cancellationToken = default)
    {
        return await readContext.Accounts
            .AnyAsync(a => a.AccountId == accountId, cancellationToken)
            .ConfigureAwait(false);
    }
}
