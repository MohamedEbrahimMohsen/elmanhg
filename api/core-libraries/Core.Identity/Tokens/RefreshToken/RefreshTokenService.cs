using Core.Errors;
using Core.Identity.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Core.Identity.Tokens.RefreshToken;

public class RefreshTokenService<TUser, TKey>(SignInManager<TUser> signInManager, IOptionsMonitor<BearerTokenOptions> bearerOptions, IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider) : IRefreshTokenService<TUser, TKey> where TUser : IdentityUser<TKey>, new() where TKey : IEquatable<TKey>
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<string> GenerateTokenAsync(TUser user, CancellationToken cancellationToken)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user).ConfigureAwait(false);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = timeProvider.GetUtcNow().AddDays(_jwt.RefreshTokenExpirationDays)
        };

        var refreshToken = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector
                                        .Protect(new AuthenticationTicket(principal, authProperties, IdentityConstants.BearerScheme));

        return refreshToken;
    }

    public async Task<TUser> ValidateTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenIsRequired);
        }

        var ticket = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector.Unprotect(refreshToken);

        if (ticket?.Properties?.ExpiresUtc is null || ticket.Properties.ExpiresUtc < timeProvider.GetUtcNow())
        {
            throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenIsExpired);
        }

        var user = await signInManager.ValidateSecurityStampAsync(ticket.Principal).ConfigureAwait(false);

        if (user is null)
        {
            throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenUserNotFound);
        }

        return user;
    }
}
