using Core.DDD.Entities;
using Core.DDD.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Core.EntityFrameworkCore.Auditing;

// Runs inside DbContext.SaveChangesAsync, after CoreDbContext has published domain events, so rows that event handlers add or change are stamped too.
public sealed class AuditStampingInterceptor(ICurrentUser? currentUser = null, TimeProvider? timeProvider = null) : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        var actorId = currentUser?.UserId;

        foreach (var entry in context.ChangeTracker.Entries<AuditEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreationDate = now;
                entry.Entity.CreatedBy ??= actorId;
            }

            if ((entry.State is EntityState.Modified or EntityState.Deleted) && !entry.Property(x => x.UpdationDate).IsModified)
            {
                entry.Property(x => x.UpdationDate).CurrentValue = now;
            }

            if (actorId is not null && entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                entry.Property(x => x.UpdatedBy).CurrentValue = actorId;
            }
        }
    }
}
