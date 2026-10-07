using Core.DDD.Entities;
using Core.DDD.Identity;
using Core.EntityFrameworkCore.Context;
using Core.Notifications.Entities;
using Core.Notifications.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Core.EntityFrameworkCore.Repositories;

public class NotificationRepository<TUser, TRole, TKey, TContext>(TContext context, ICurrentUser? currentUser = null, TimeProvider? timeProvider = null) : Repository<Notification>(context, currentUser, timeProvider), INotificationRepository
    where TUser : IdentityUser<TKey>, IEntity, new()
    where TRole : IdentityRole<TKey>, new()
    where TKey : IEquatable<TKey>, new()
    where TContext : CoreDbContext<TUser, TRole, TKey>
{
    
}

