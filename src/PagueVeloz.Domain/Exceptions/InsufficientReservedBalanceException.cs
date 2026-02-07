namespace PagueVeloz.Domain.Exceptions;

public sealed class InsufficientReservedBalanceException(string accountId, long requested, long reserved) : DomainException("INSUFFICIENT_RESERVED_BALANCE",
        $"Account {accountId} has insufficient reserved balance. Requested: {requested}, Reserved: {reserved}")
{
}
