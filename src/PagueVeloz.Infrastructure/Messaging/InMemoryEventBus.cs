using Microsoft.Extensions.Logging;
using PagueVeloz.Application.Interfaces;
using PagueVeloz.Domain.Events;

namespace PagueVeloz.Infrastructure.Messaging;

public sealed class InMemoryEventBus(ILogger<InMemoryEventBus> logger) : IEventBus
{
    public ValueTask PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Event published: {EventType} (ID: {EventId}) at {OccurredAt}",
            domainEvent.GetType().Name,
            domainEvent.EventId,
            domainEvent.OccurredAt);

        return ValueTask.CompletedTask;
    }

    public async ValueTask PublishAllAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await PublishAsync(domainEvent, cancellationToken).ConfigureAwait(false);
        }
    }
}
