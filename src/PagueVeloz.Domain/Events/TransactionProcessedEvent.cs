using PagueVeloz.Domain.Enums;

namespace PagueVeloz.Domain.Events;

public sealed record TransactionProcessedEvent(
    Guid TransactionId,
    string AccountId,
    TransactionType Type,
    long Amount,
    string Currency,
    TransactionStatus Status,
    long AvailableBalance,
    long ReservedBalance
) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
