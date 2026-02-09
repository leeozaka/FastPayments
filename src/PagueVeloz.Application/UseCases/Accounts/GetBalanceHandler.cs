using MediatR;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Domain.Exceptions;
using PagueVeloz.Domain.Interfaces.Repositories;

namespace PagueVeloz.Application.UseCases.Accounts;

public sealed class GetBalanceHandler( IAccountRepository accountRepository, ICacheService cacheService) : IRequestHandler<GetBalanceQuery, AccountResponse>
{
    private static readonly TimeSpan BalanceCacheTtl = TimeSpan.FromSeconds(5);

    public async Task<AccountResponse> Handle(GetBalanceQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.AccountBalance(request.AccountId);

        return await cacheService.GetOrCreateAsync(cacheKey, async ct =>
        {
            var account = await accountRepository
                .GetByAccountIdReadOnlyAsync(request.AccountId, ct)
                .ConfigureAwait(false)
                ?? throw new DomainException("ACCOUNT_NOT_FOUND", $"Account '{request.AccountId}' not found.");

            return new AccountResponse
            {
                AccountId = account.AccountId,
                ClientId = account.ClientId,
                Balance = account.Balance,
                ReservedBalance = account.ReservedBalance,
                AvailableBalance = account.AvailableBalance,
                CreditLimit = account.CreditLimit,
                Status = account.Status.ToString().ToLowerInvariant(),
                Currency = account.CurrencyCode
            };
        }, BalanceCacheTtl, cancellationToken).ConfigureAwait(false);
    }
}
