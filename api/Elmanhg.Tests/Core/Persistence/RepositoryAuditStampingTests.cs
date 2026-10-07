using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class RepositoryAuditStampingTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Earlier = Now.AddHours(-3);

    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _creatorId = Guid.NewGuid();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly AuditStampingProbeDbContext _context = new();

    public RepositoryAuditStampingTests()
    {
        _currentUser.UserId.Returns(_actorId);
        _timeProvider.GetUtcNow().Returns(Now);
    }

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
    public async Task SaveChangesAsync_AddedWithCreator_KeepsCreatedBy()
    {
        var probe = new StampedProbe(_creatorId);
        var repository = Repository();
        await repository.AddAsync(probe, TestContext.Current.CancellationToken);

        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        (probe.CreatedBy, probe.UpdatedBy).Should().Be((_creatorId, _creatorId));
    }

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
    public async Task SaveChangesAsync_ModifiedWithUpdatedBySet_KeepsAggregateActor()
    {
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
    public async Task SaveChangesAsync_UnchangedEntity_LeavesStampsUnchanged()
    {
        var probe = AttachedProbe();

        await Repository().SaveChangesAsync(TestContext.Current.CancellationToken);

        (probe.UpdationDate, probe.UpdatedBy).Should().Be((Earlier, _creatorId));
    }

    public void Dispose() => _context.Dispose();

    private Repository<StampedProbe> Repository() => new(_context, _currentUser, _timeProvider);

    private StampedProbe AttachedProbe()
    {
        var probe = new StampedProbe(_creatorId) { UpdationDate = Earlier, UpdatedBy = _creatorId };
        _context.Attach(probe);
        return probe;
    }
}
