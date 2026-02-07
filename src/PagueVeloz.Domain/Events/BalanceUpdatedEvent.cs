namespace PagueVeloz.Domain.Events;

public sealed record BalanceUpdatedEvent(
    string AccountId,
    long PreviousBalance,
    long NewBalance,
    long ReservedBalance,
    long AvailableBalance
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
