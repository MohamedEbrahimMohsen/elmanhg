using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class AuditStampingInterceptorTests : AuditStampingTestBase
{
    [Fact]
    public async Task SaveChangesAsync_AddedEntity_StampsCreationDateFromTimeProvider()
    {
        var probe = new StampedProbe(_creatorId);
        var repository = Repository();
        await repository.AddAsync(probe, TestContext.Current.CancellationToken);

        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        probe.CreationDate.Should().Be(Now);
    }

    [Fact]
    public async Task SaveChangesAsync_AddedWithoutCreator_StampsCreatedByAndUpdatedByFromCurrentUser()
    {
        var probe = new StampedProbe(null);
        var repository = Repository();
        await repository.AddAsync(probe, TestContext.Current.CancellationToken);

        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        (probe.CreatedBy, probe.UpdatedBy).Should().Be((_actorId, _actorId));
    }

    [Fact]
    public async Task SaveChangesAsync_AddedWithCreator_KeepsCreatedByAndStampsActingUserAsUpdatedBy()
    {
        var probe = new StampedProbe(_creatorId);
        var repository = Repository();
        await repository.AddAsync(probe, TestContext.Current.CancellationToken);

        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        (probe.CreatedBy, probe.UpdatedBy).Should().Be((_creatorId, _actorId));
    }

    [Fact]
    public async Task SaveChangesAsync_AddedWithCreatorAndNoCurrentUser_KeepsCreatorAsUpdatedBy()
    {
        _currentUser.UserId.Returns((Guid?)null);
        var probe = new StampedProbe(_creatorId);
        var repository = Repository();
        await repository.AddAsync(probe, TestContext.Current.CancellationToken);

        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        (probe.CreatedBy, probe.UpdatedBy).Should().Be((_creatorId, _creatorId));
    }

    [Fact]
    public async Task SaveChangesAsync_UnchangedEntity_LeavesStampsUnchanged()
    {
        var probe = AttachedProbe();

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        (probe.UpdationDate, probe.UpdatedBy).Should().Be((Earlier, _creatorId));
    }

    [Fact]
    public async Task SaveChangesAsync_RowAddedByDomainEventHandler_StampsActingUserAndCreationDate()
    {
        var added = new StampedProbe(null);
        var probe = AttachedProbe();
        probe.Name = "second";
        probe.RaiseDomainEvent(new DispatchProbeRaised(1));
        OnPublish(() => _context.Probes.Add(added));

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        (added.CreatedBy, added.UpdatedBy, added.CreationDate).Should().Be((_actorId, _actorId, Now));
    }

    [Fact]
    public async Task SaveChangesAsync_RowChangedByDomainEventHandler_StampsActingUserAndUpdationDate()
    {
        var probe = AttachedProbe();
        var changed = AttachedProbe();
        probe.Name = "second";
        probe.RaiseDomainEvent(new DispatchProbeRaised(1));
        OnPublish(() => changed.Name = "changed");

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        (changed.UpdatedBy, changed.UpdationDate).Should().Be((_actorId, Now));
    }
}
