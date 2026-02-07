using PagueVeloz.Domain.Entities;

namespace PagueVeloz.Domain.Interfaces.Services;

public interface ITransferService
{
    Task<(Transaction DebitTransaction, Transaction CreditTransaction)> TransferAsync(
        string sourceAccountId,
        string destinationAccountId,
        long amount,
        string currency,
        string referenceId,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);
}
