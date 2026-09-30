using Core.Errors;
using Core.Identity.Tokens;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Application.Auth.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Auth.RefreshAccessToken;

public sealed class RefreshAccessTokenHandler(ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService, IIssuedRefreshTokenRepository issuedRefreshTokenRepository, IOptions<AuthOptions> authOptions, IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider) : IRequestHandler<RefreshAccessTokenCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RefreshAccessTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await refreshTokenService.ValidateTokenAsync(request.RefreshToken, cancellationToken).ConfigureAwait(false);
        if (!user.IsActive)
        {
            throw new ForbiddenCoreException(ErrorCodes.UserSuspended);
        }

        var now = timeProvider.GetUtcNow();
        var presentedHash = RefreshTokenHash.Compute(request.RefreshToken);
        var presented = await issuedRefreshTokenRepository.FindAsync(x => x.TokenHash == presentedHash, cancellationToken).ConfigureAwait(false);
        if (presented.Any(x => x.IsRevoked))
        {
            throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenRevoked);
        }

        if (presented.Any(x => x.IsReplayed(now, TimeSpan.FromSeconds(authOptions.Value.RefreshTokenReuseGraceSeconds))))
        {
            var familyIds = presented
                .Select(x => x.FamilyId)
                .Distinct()
                .ToList();
            var family = await issuedRefreshTokenRepository.FindAsync(x => familyIds.Contains(x.FamilyId) && x.RevokedAt == null, cancellationToken).ConfigureAwait(false);
            family.ForEach(x => x.Revoke(now));
            await issuedRefreshTokenRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenRevoked);
        }

        var expiresAt = now.AddDays(jwtOptions.Value.RefreshTokenExpirationDays);
        if (presented.Count == 0)
        {
            var loginToken = IssuedRefreshToken.Issue(user.Id, Guid.NewGuid(), presentedHash, now, expiresAt);
            await issuedRefreshTokenRepository.AddAsync(loginToken, cancellationToken).ConfigureAwait(false);
            presented.Add(loginToken);
        }

        presented.ForEach(x => x.Rotate(now));
        var accessToken = tokenService.GenerateTokenAsync(user.GetUserClaims());
        var refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken).ConfigureAwait(false);
        await issuedRefreshTokenRepository.AddAsync(IssuedRefreshToken.Issue(user.Id, presented[0].FamilyId, RefreshTokenHash.Compute(refreshToken), now, expiresAt), cancellationToken).ConfigureAwait(false);
        await issuedRefreshTokenRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return AuthResultGenerator.Generate(user, accessToken, refreshToken);
    }
}
