using Core.DDD.Entities;
using MediatR;

namespace Core.EntityFrameworkCore.Context;

public static class DomainEventDispatcher
{
    public const int MaxRounds = 10;

    // Each round drains events before publishing, so events a handler raises, or raises on entities it adds, are published by the next round.
    public static async Task PublishAsync(IMediator mediator, Func<IEnumerable<Entity>> trackedEntities, CancellationToken cancellationToken)
    {
        for (var round = 0; round < MaxRounds; round++)
        {
            var domainEvents = trackedEntities().ToList().SelectMany(Drain).ToList();
            if (domainEvents.Count == 0)
            {
                return;
            }

            foreach (var domainEvent in domainEvents)
            {
                await mediator.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException($"Domain event handlers were still raising events after {MaxRounds} rounds.");
    }

    private static List<DomainEvent> Drain(Entity entity)
    {
        List<DomainEvent> domainEvents = [.. entity.GetDomainEvents()];
        entity.ClearDomainEvents();
        return domainEvents;
    }
}
