namespace PagueVeloz.Domain.Exceptions;

public sealed class InsufficientFundsException(string accountId, long requested, long available) : DomainException("INSUFFICIENT_FUNDS",
        $"Account {accountId} has insufficient funds. Requested: {requested}, Available: {available}")
{
}
