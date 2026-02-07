namespace PagueVeloz.Domain.Exceptions;

public sealed class DuplicateTransactionException(string referenceId) : DomainException("DUPLICATE_TRANSACTION", $"Transaction with reference_id '{referenceId}' already exists.")
{
}
