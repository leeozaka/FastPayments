using PagueVeloz.Domain.Events;

namespace PagueVeloz.Application.Interfaces;

public interface IEventBus
{
    ValueTask PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
    ValueTask PublishAllAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
