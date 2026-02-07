using MediatR;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Domain.Exceptions;
using PagueVeloz.Domain.Interfaces.Repositories;

namespace PagueVeloz.Application.UseCases.Accounts;

public sealed class GetBalanceHandler(IAccountRepository accountRepository) : IRequestHandler<GetBalanceQuery, AccountResponse>
{
    public async Task<AccountResponse> Handle(GetBalanceQuery request, CancellationToken cancellationToken)
    {
        var account = await accountRepository
            .GetByAccountIdAsync(request.AccountId, cancellationToken)
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
    }
}
