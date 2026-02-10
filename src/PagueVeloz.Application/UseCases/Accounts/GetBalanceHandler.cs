using Ardalis.Result;
using MediatR;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Application.Mappers;
using PagueVeloz.Domain.Interfaces.Repositories;

namespace PagueVeloz.Application.UseCases.Accounts;

public sealed class GetBalanceHandler(IAccountRepository accountRepository, ICacheService cacheService) : IRequestHandler<GetBalanceQuery, Result<AccountResponse>>
{
    private static readonly TimeSpan BalanceCacheTtl = TimeSpan.FromSeconds(5);

    public async Task<Result<AccountResponse>> Handle(GetBalanceQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = CacheKeys.AccountBalance(request.AccountId);

        var cached = await cacheService.GetAsync<AccountResponse>(cacheKey, cancellationToken)
            .ConfigureAwait(false);

        if (cached is not null)
            return Result.Success(cached);

        var account = await accountRepository
            .GetByAccountIdReadOnlyAsync(request.AccountId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
            return Result.NotFound($"Account '{request.AccountId}' not found.");

        var response = account.ToResponse();

        await cacheService.SetAsync(cacheKey, response, BalanceCacheTtl, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(response);
    }
}
