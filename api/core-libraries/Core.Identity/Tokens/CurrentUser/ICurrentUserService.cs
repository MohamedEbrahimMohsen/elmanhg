using Core.DDD.Identity;

namespace Core.Identity.Tokens.CurrentUser;

public interface ICurrentUserService : ICurrentUser
{
    string? PhoneNumber { get; }
    long? CreatedAtUnixTimeSeconds { get; }
    string? GetClaim(string claimName);
}
