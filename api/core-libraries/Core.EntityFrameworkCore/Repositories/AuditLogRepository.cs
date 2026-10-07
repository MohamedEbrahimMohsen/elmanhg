using Core.Auditing.Entities;
using Core.Auditing.Repositories;
using Core.DDD.Entities;
using Core.DDD.Identity;
using Core.EntityFrameworkCore.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Core.EntityFrameworkCore.Repositories;

public class AuditLogRepository<TUser, TRole, TKey, TContext>(TContext context, ICurrentUser? currentUser = null, TimeProvider? timeProvider = null) : Repository<AuditLog>(context, currentUser, timeProvider), IAuditLogRepository
    where TUser : IdentityUser<TKey>, IEntity, new()
    where TRole : IdentityRole<TKey>, new()
    where TKey : IEquatable<TKey>, new()
    where TContext : CoreDbContext<TUser, TRole, TKey>
{
    public async Task AppendAsync(AuditLog entry, CancellationToken cancellationToken)
    {
        await _context.Database.ExecuteSqlAsync($"""INSERT INTO "AuditLogs" ("Id", "Timestamp", "ActorUserId", "ActorUserName", "ActorRole", "Action", "ResourceType", "ResourceId", "Outcome", "ErrorCode", "TraceId", "Diff", "IsDeleted", "DeletedAt") VALUES ({entry.Id}, {entry.Timestamp}, {entry.ActorUserId}, {entry.ActorUserName}, {entry.ActorRole}, {entry.Action}, {entry.ResourceType}, {entry.ResourceId}, {entry.Outcome}, {entry.ErrorCode}, {entry.TraceId}, CAST({entry.Diff} AS jsonb), FALSE, NULL)""", cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<string>> GetResourceTypesAsync(CancellationToken cancellationToken)
    {
        return await _dbSet.AsNoTracking()
            .Select(x => x.ResourceType)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
