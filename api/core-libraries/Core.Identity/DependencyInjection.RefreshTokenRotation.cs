using Core.Identity.Tokens.RefreshToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Core.Identity;

public static partial class DependencyInjection
{
    public static IServiceCollection AddCoreRefreshTokenRotation<TUser>(this IServiceCollection services) where TUser : IdentityUser<Guid>, new()
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<RefreshTokenRotationOptions>()
            .Validate(x => x.ReuseGrace >= TimeSpan.Zero, "Refresh-token reuse grace must not be negative.")
            .ValidateOnStart();
        services.AddScoped<IRefreshTokenRotator<TUser>, RefreshTokenRotator<TUser>>();
        return services;
    }
}
