using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Exceptions;
using PagueVeloz.Domain.Interfaces.Repositories;
using PagueVeloz.Domain.Interfaces.Services;

namespace PagueVeloz.Domain.Services;

public sealed class TransferDomainService(IAccountRepository accountRepository) : ITransferService
{
    public async Task<(Transaction DebitTransaction, Transaction CreditTransaction)> TransferAsync(
        string sourceAccountId,
        string destinationAccountId,
        long amount,
        string currency,
        string referenceId,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        if (sourceAccountId == destinationAccountId)
            throw new DomainException("SAME_ACCOUNT", "Cannot transfer to the same account.");

        var source = await accountRepository
            .GetByAccountIdWithLockAsync(sourceAccountId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("ACCOUNT_NOT_FOUND", $"Source account '{sourceAccountId}' not found.");

        var destination = await accountRepository
            .GetByAccountIdWithLockAsync(destinationAccountId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException("ACCOUNT_NOT_FOUND", $"Destination account '{destinationAccountId}' not found.");

        var debitTx = source.Debit(amount, currency, $"{referenceId}-DEBIT", metadata);

        if (debitTx.Status == Enums.TransactionStatus.Failed)
        {
            return (debitTx, Transaction.Create(
                destinationAccountId, Enums.TransactionType.Credit, amount, currency,
                $"{referenceId}-CREDIT", Enums.TransactionStatus.Failed, metadata,
                "Transfer debit failed"));
        }

        var creditTx = destination.Credit(amount, currency, $"{referenceId}-CREDIT", metadata);

        await accountRepository.UpdateAsync(source, cancellationToken).ConfigureAwait(false);
        await accountRepository.UpdateAsync(destination, cancellationToken).ConfigureAwait(false);

        return (debitTx, creditTx);
    }
}
