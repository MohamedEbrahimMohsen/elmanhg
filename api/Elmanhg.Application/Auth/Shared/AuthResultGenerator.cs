using Elmanhg.Domain.Identity;

namespace Elmanhg.Application.Auth.Shared;

public static class AuthResultGenerator
{
    public static AuthResult Generate(User user, string accessToken, string refreshToken)
    {
        return new AuthResult(accessToken, new AuthUserResult(user.Id, user.DisplayName, user.Role.ToString(), user.PhoneNumber, user.Email), refreshToken);
    }
}
