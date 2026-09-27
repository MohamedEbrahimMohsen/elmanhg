using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Domain.Identity;
using System.Globalization;
using System.Security.Claims;

namespace Elmanhg.Application.Auth.Shared;

public static class UserClaimsExtensions
{
    public static List<Claim> GetUserClaims(this User user)
    {
        List<Claim> claims =
        [
            new(CurrentUserService.Constants.UserIdClaimType, user.Id.ToString("D")),
            new(CurrentUserService.Constants.UserNameClaimType, user.UserName ?? string.Empty),
            new(CurrentUserService.Constants.CreatedAtUnixTimeSecondsClaimType, user.CreationDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Role, user.Role.ToString()),
        ];

        if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            claims.Add(new(CurrentUserService.Constants.PhoneNumberClaimType, user.PhoneNumber));
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new(ClaimTypes.Email, user.Email));
        }

        return claims;
    }
}
