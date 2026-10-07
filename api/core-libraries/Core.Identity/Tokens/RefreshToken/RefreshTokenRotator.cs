using Core.Errors;
using Core.Identity.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Core.Identity.Tokens.RefreshToken;

public sealed class RefreshTokenRotator<TUser>(IRefreshTokenService<TUser, Guid> refreshTokenService, IIssuedRefreshTokenRepository issuedRefreshTokenRepository, IOptions<RefreshTokenRotationOptions> rotationOptions, IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider) : IRefreshTokenRotator<TUser> where TUser : IdentityUser<Guid>, new()
{
    public async Task<string> RotateAsync(TUser user, string presentedRefreshToken, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var presentedHash = RefreshTokenHash.Compute(presentedRefreshToken);
        var presented = await issuedRefreshTokenRepository.FindAsync(x => x.TokenHash == presentedHash, cancellationToken).ConfigureAwait(false);
        if (presented.Any(x => x.IsRevoked))
        {
            throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenRevoked);
        }

        if (presented.Any(x => x.IsReplayed(now, rotationOptions.Value.ReuseGrace)))
        {
            await RevokeFamiliesAsync(presented, now, cancellationToken).ConfigureAwait(false);
            throw new UnauthorizedCoreException(ErrorCodes.RefreshTokenRevoked);
        }

        var expiresAt = now.AddDays(jwtOptions.Value.RefreshTokenExpirationDays);
        if (presented.Count == 0)
        {
            await issuedRefreshTokenRepository.AddIfAbsentAsync(IssuedRefreshToken.Issue(user.Id, Guid.NewGuid(), presentedHash, now, expiresAt), cancellationToken).ConfigureAwait(false);
            presented = await issuedRefreshTokenRepository.FindAsync(x => x.TokenHash == presentedHash, cancellationToken).ConfigureAwait(false);
        }

        presented.ForEach(x => x.Rotate(now));
        var refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken).ConfigureAwait(false);
        await issuedRefreshTokenRepository.AddAsync(IssuedRefreshToken.Issue(user.Id, presented[0].FamilyId, RefreshTokenHash.Compute(refreshToken), now, expiresAt), cancellationToken).ConfigureAwait(false);
        await issuedRefreshTokenRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return refreshToken;
    }

    private async Task RevokeFamiliesAsync(List<IssuedRefreshToken> presented, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var familyIds = presented
            .Select(x => x.FamilyId)
            .Distinct()
            .ToList();
        var family = await issuedRefreshTokenRepository.FindAsync(x => familyIds.Contains(x.FamilyId) && x.RevokedAt == null, cancellationToken).ConfigureAwait(false);
        family.ForEach(x => x.Revoke(now));
        await issuedRefreshTokenRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
