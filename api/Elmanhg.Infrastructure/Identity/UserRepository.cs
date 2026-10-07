using Core.DDD.Identity;
using Core.EntityFrameworkCore.Repositories;
using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Infrastructure.Identity;

public class UserRepository(AppDbContext context, ICurrentUser currentUser, TimeProvider timeProvider) : Repository<User>(context, currentUser, timeProvider), IUserRepository
{
    // Serialises admin deactivations so two admins cannot deactivate each other past the last-admin rule; any fixed key unique to this lock.
    private const long AdminRosterLockKey = 106001;

    public async Task ExecuteInAdminRosterLockAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({AdminRosterLockKey})", cancellationToken).ConfigureAwait(false);
            await operation(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
