using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Application.Auth.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using MediatR;

namespace Elmanhg.Application.Auth.RefreshAccessToken;

public sealed class RefreshAccessTokenHandler(ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService) : IRequestHandler<RefreshAccessTokenCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RefreshAccessTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await refreshTokenService.ValidateTokenAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);
        if (!user.IsActive)
        {
            throw new ForbiddenCoreException(ErrorCodes.UserSuspended);
        }

        var accessToken = tokenService.GenerateTokenAsync(user.GetUserClaims());
        var refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken).ConfigureAwait(false);
        return AuthResultGenerator.Generate(user, accessToken, refreshToken);
    }
}
