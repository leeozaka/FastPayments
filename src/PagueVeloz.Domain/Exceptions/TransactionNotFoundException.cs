namespace PagueVeloz.Domain.Exceptions;

public sealed class TransactionNotFoundException(string referenceId) : DomainException("TRANSACTION_NOT_FOUND", $"Transaction with reference_id '{referenceId}' not found.")
{
}
