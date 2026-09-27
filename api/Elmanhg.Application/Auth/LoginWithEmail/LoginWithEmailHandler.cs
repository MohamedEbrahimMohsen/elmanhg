using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Application.Auth.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Application.Auth.LoginWithEmail;

public sealed class LoginWithEmailHandler(UserManager<User> userManager, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService) : IRequestHandler<LoginWithEmailCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginWithEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
        if (user is null)
        {
            throw new BadRequestCoreException(ErrorCodes.UserInvalidLogin);
        }

        if (await userManager.IsLockedOutAsync(user).ConfigureAwait(false))
        {
            throw new RateLimitExceededCoreException(ErrorCodes.UserLockedOut);
        }

        if (!await userManager.CheckPasswordAsync(user, request.Password).ConfigureAwait(false))
        {
            await userManager.AccessFailedAsync(user).ConfigureAwait(false);
            throw new BadRequestCoreException(ErrorCodes.UserInvalidLogin);
        }

        if (!user.IsActive)
        {
            throw new ForbiddenCoreException(ErrorCodes.UserSuspended);
        }

        await userManager.ResetAccessFailedCountAsync(user).ConfigureAwait(false);

        var accessToken = tokenService.GenerateTokenAsync(user.GetUserClaims());
        var refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken).ConfigureAwait(false);
        return AuthResultGenerator.Generate(user, accessToken, refreshToken);
    }
}
