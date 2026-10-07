using Microsoft.AspNetCore.Identity;

namespace Core.Identity.Tokens.RefreshToken;

public interface IRefreshTokenRotator<TUser> where TUser : IdentityUser<Guid>, new()
{
    Task<string> RotateAsync(TUser user, string presentedRefreshToken, CancellationToken cancellationToken);
}
