using MediatR;
using PagueVeloz.Application.DTOs;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Interfaces.Repositories;

namespace PagueVeloz.Application.UseCases.Accounts;

public sealed class CreateAccountHandler(
    IAccountRepository accountRepository,
    IClientRepository clientRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateAccountCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var client = await clientRepository
            .GetByClientIdAsync(request.ClientId, cancellationToken)
            .ConfigureAwait(false);

        if (client is null)
        {
            client = Client.Create(request.ClientId, request.ClientId);
            await clientRepository.AddAsync(client, cancellationToken).ConfigureAwait(false);
        }

        var accountId = request.AccountId ?? $"ACC-{Guid.NewGuid():N}"[..10].ToUpperInvariant();

        var account = Account.Create(accountId, request.ClientId, request.CreditLimit, request.Currency);

        await accountRepository.AddAsync(account, cancellationToken).ConfigureAwait(false);

        if (request.InitialBalance > 0)
        {
            account.Credit(request.InitialBalance, request.Currency, $"{accountId}-INIT");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
