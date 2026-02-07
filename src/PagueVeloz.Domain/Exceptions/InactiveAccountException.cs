namespace PagueVeloz.Domain.Exceptions;

public sealed class InactiveAccountException(string accountId) : DomainException("INACTIVE_ACCOUNT", $"Account {accountId} is not active.")
{
}
