using Core.DDD.Entities;
using Core.EntityFrameworkCore.Context;
using FluentAssertions;
using MediatR;
using NSubstitute;

namespace Elmanhg.Tests.Core.Persistence;

public sealed record DispatchProbeRaised(int Step) : DomainEvent;

public sealed class DispatchProbeEntity() : Entity(Guid.NewGuid());

public sealed class DomainEventDispatcherTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly List<DomainEvent> _published = [];
    private readonly List<Entity> _tracked = [];

    public DomainEventDispatcherTests() => _mediator.Publish(Arg.Do<DomainEvent>(_published.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

    [Fact]
    public async Task PublishAsync_TrackedEvents_PublishesEachOnceAndClearsThem()
    {
        var entity = Track(new DispatchProbeEntity());
        entity.RaiseDomainEvent(new DispatchProbeRaised(1));
        entity.RaiseDomainEvent(new DispatchProbeRaised(2));

        await DomainEventDispatcher.PublishAsync(_mediator, () => _tracked, TestContext.Current.CancellationToken);

        _published.Should().Equal(new DispatchProbeRaised(1), new DispatchProbeRaised(2));
        entity.GetDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public async Task PublishAsync_HandlerRaisesOnTrackedAndAddedEntities_PublishesThemInLaterRounds()
    {
        var entity = Track(new DispatchProbeEntity());
        entity.RaiseDomainEvent(new DispatchProbeRaised(1));
        OnPublish(step =>
        {
            if (step == 1)
            {
                entity.RaiseDomainEvent(new DispatchProbeRaised(2));
                Track(new DispatchProbeEntity()).RaiseDomainEvent(new DispatchProbeRaised(3));
            }
        });

        await DomainEventDispatcher.PublishAsync(_mediator, () => _tracked, TestContext.Current.CancellationToken);

        _published.Should().Equal(new DispatchProbeRaised(1), new DispatchProbeRaised(2), new DispatchProbeRaised(3));
        _tracked.Should().AllSatisfy(x => x.GetDomainEvents().Should().BeEmpty());
    }

    [Fact]
    public async Task PublishAsync_HandlersKeepRaising_ThrowsAfterMaxRounds()
    {
        var entity = Track(new DispatchProbeEntity());
        entity.RaiseDomainEvent(new DispatchProbeRaised(1));
        OnPublish(step => entity.RaiseDomainEvent(new DispatchProbeRaised(step + 1)));

        var act = () => DomainEventDispatcher.PublishAsync(_mediator, () => _tracked, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _published.Should().HaveCount(DomainEventDispatcher.MaxRounds);
    }

    private DispatchProbeEntity Track(DispatchProbeEntity entity)
    {
        _tracked.Add(entity);
        return entity;
    }

    private void OnPublish(Action<int> handler) => _mediator.When(x => x.Publish(Arg.Any<DomainEvent>(), Arg.Any<CancellationToken>())).Do(call => handler(((DispatchProbeRaised)call.Arg<DomainEvent>()).Step));
}
