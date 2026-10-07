using Core.DDD.Entities;
using Core.DDD.Identity;
using Core.EntityFrameworkCore.Auditing;
using Core.EntityFrameworkCore.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Elmanhg.Tests.Core.Persistence;

public sealed class StampedProbe : AuditEntity
{
    public StampedProbe(Guid? createdBy) : base(Guid.NewGuid(), createdBy) { }

    public string Name { get; set; } = "first";
}

public sealed class AuditStampingProbeDbContext(ICurrentUser? currentUser, TimeProvider timeProvider, IMediator mediator) : DbContext(Options(currentUser, timeProvider))
{
    public DbSet<StampedProbe> Probes { get; set; }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DomainEventDispatcher.PublishAsync(mediator, () => ChangeTracker.Entries<Entity>().Select(x => x.Entity), cancellationToken).ConfigureAwait(false);
        return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DbContextOptions<AuditStampingProbeDbContext> Options(ICurrentUser? currentUser, TimeProvider timeProvider) =>
        new DbContextOptionsBuilder<AuditStampingProbeDbContext>()
            .UseNpgsql("Host=localhost;Database=audit-stamping-probe")
            .AddInterceptors(new AuditStampingInterceptor(currentUser, timeProvider), new SuppressedSaveInterceptor())
            .Options;
}

public sealed class SuppressedSaveInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) => InterceptionResult<int>.SuppressWithResult(0);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
}

public sealed class AuditStampingRegistrationProbeDbContext(DbContextOptions<AuditStampingRegistrationProbeDbContext> options) : DbContext(options)
{
    public DbSet<StampedProbe> Probes { get; set; }
}
