using Core.DDD.Entities;
using System.Collections.Concurrent;

namespace Elmanhg.Tests.Integration.Infrastructure;

public sealed class LessonEventLog
{
    private readonly ConcurrentQueue<DomainEvent> _events = new();

    public IReadOnlyList<DomainEvent> Events => [.. _events];

    public void Record(DomainEvent domainEvent)
    {
        _events.Enqueue(domainEvent);
    }
}
