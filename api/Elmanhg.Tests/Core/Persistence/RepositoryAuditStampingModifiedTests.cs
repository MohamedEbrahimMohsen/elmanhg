using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class RepositoryAuditStampingModifiedTests : AuditStampingTestBase
{
    [Fact]
    public async Task SaveChangesAsync_ModifiedWithoutUpdationDate_StampsUpdationDateFromTimeProvider()
    {
        var probe = AttachedProbe();
        probe.Name = "second";

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        probe.UpdationDate.Should().Be(Now);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedWithUpdationDateSet_KeepsAggregateInstant()
    {
        var probe = AttachedProbe();
        probe.Name = "second";
        probe.UpdationDate = Earlier.AddMinutes(1);

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        probe.UpdationDate.Should().Be(Earlier.AddMinutes(1));
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedWithoutUpdatedBy_StampsUpdatedByFromCurrentUser()
    {
        var probe = AttachedProbe();
        probe.Name = "second";

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        probe.UpdatedBy.Should().Be(_actorId);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedWithUpdatedBySet_StampsActingUser()
    {
        var probe = AttachedProbe();
        probe.Name = "second";
        probe.UpdatedBy = Guid.NewGuid();

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        probe.UpdatedBy.Should().Be(_actorId);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedWithUpdatedBySetToItsCurrentValue_StampsActingUser()
    {
        var probe = AttachedProbe();
        probe.Name = "second";
        probe.UpdatedBy = _creatorId;

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        probe.UpdatedBy.Should().Be(_actorId);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedWithUpdatedBySetAndNoCurrentUser_KeepsAggregateActor()
    {
        _currentUser.UserId.Returns((Guid?)null);
        var other = Guid.NewGuid();
        var probe = AttachedProbe();
        probe.Name = "second";
        probe.UpdatedBy = other;

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        probe.UpdatedBy.Should().Be(other);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedWithoutCurrentUser_LeavesUpdatedByUnchanged()
    {
        _currentUser.UserId.Returns((Guid?)null);
        var probe = AttachedProbe();
        probe.Name = "second";

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        (probe.UpdatedBy, probe.UpdationDate).Should().Be((_creatorId, Now));
    }

    [Fact]
    public void SaveChanges_SynchronousSave_StampsActingUserAndUpdationDate()
    {
        var probe = AttachedProbe();
        probe.Name = "second";

        _context.SaveChanges();

        (probe.UpdatedBy, probe.UpdationDate).Should().Be((_actorId, Now));
    }
}
