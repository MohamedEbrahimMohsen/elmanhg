using Elmanhg.Application.Shared.Options;

namespace Elmanhg.Api.Controllers.Auth;

public static class RefreshTokenCookieExtensions
{
    public static void AppendRefreshToken(this HttpResponse response, string refreshToken, AuthOptions options, int expirationDays)
    {
        response.Cookies.Append(options.RefreshTokenCookieName, refreshToken, Build(options, DateTimeOffset.UtcNow.AddDays(expirationDays)));
    }

    public static void DeleteRefreshToken(this HttpResponse response, AuthOptions options)
    {
        response.Cookies.Delete(options.RefreshTokenCookieName, Build(options, null));
    }

    private static CookieOptions Build(AuthOptions options, DateTimeOffset? expires)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = options.RefreshTokenCookieSecure,
            SameSite = SameSiteMode.Strict,
            Path = options.RefreshTokenCookiePath,
            Expires = expires,
            IsEssential = true,
        };
    }
}
