using Core.DDD.Entities;
using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using MediatR;
using NSubstitute;

namespace Elmanhg.Tests.Core.Persistence;

public abstract class AuditStampingTestBase : IDisposable
{
    protected static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    protected static readonly DateTimeOffset Earlier = Now.AddHours(-3);

    protected readonly Guid _actorId = Guid.NewGuid();
    protected readonly Guid _creatorId = Guid.NewGuid();
    protected readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    protected readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    protected readonly IMediator _mediator = Substitute.For<IMediator>();
    protected readonly AuditStampingProbeDbContext _context;

    protected AuditStampingTestBase()
    {
        _currentUser.UserId.Returns(_actorId);
        _timeProvider.GetUtcNow().Returns(Now);
        _context = new(_currentUser, _timeProvider, _mediator);
    }

    public void Dispose() => _context.Dispose();

    protected Repository<StampedProbe> Repository() => new(_context);

    protected StampedProbe AttachedProbe()
    {
        var probe = new StampedProbe(_creatorId) { UpdationDate = Earlier, UpdatedBy = _creatorId };
        _context.Attach(probe);
        return probe;
    }

    protected void OnPublish(Action handler) => _mediator.Publish(Arg.Do<DomainEvent>(_ => handler()), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
}
