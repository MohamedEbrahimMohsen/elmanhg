using Core.Auditing.Repositories;
using Core.DDD.Entities;
using Core.DDD.Repositories;
using Core.EntityFrameworkCore.Auditing;
using Core.EntityFrameworkCore.Context;
using Core.EntityFrameworkCore.Repositories;
using Core.Notifications.Repositories;
using Core.OTP.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.EntityFrameworkCore;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreEntityFrameworkCore<TUser, TRole, TKey, TContext>(this IServiceCollection services)
        where TUser : IdentityUser<TKey>, IEntity, new()
        where TRole : IdentityRole<TKey>, new()
        where TKey : IEquatable<TKey>, new()
        where TContext : CoreDbContext<TUser, TRole, TKey>
    {
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IOtpRepository, OtpRepository<TUser, TRole, TKey, TContext>>();
        services.AddScoped<IUserDeviceRepository, UserDeviceRepository<TUser, TRole, TKey, TContext>>();
        services.AddScoped<INotificationRepository, NotificationRepository<TUser, TRole, TKey, TContext>>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository<TUser, TRole, TKey, TContext>>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository<TUser, TRole, TKey, TContext>>();

        return services;
    }

    public static IServiceCollection AddCoreAuditStamping<TContext>(this IServiceCollection services) where TContext : DbContext
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<AuditStampingInterceptor>();
        services.ConfigureDbContext<TContext>((provider, options) => options.AddInterceptors(provider.GetRequiredService<AuditStampingInterceptor>()));

        return services;
    }
}