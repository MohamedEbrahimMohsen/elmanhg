using Core.DDD.Entities;

namespace Elmanhg.Domain.Identity;

public class IssuedRefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RotatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public bool IsRevoked => RevokedAt is not null;

    private IssuedRefreshToken(Guid id) : base(id) { }

    public static IssuedRefreshToken Issue(Guid userId, Guid familyId, string tokenHash, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        return new IssuedRefreshToken(Guid.NewGuid())
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
        };
    }

    public bool IsReplayed(DateTimeOffset now, TimeSpan reuseGrace) => RotatedAt is { } rotatedAt && now - rotatedAt > reuseGrace;

    public void Rotate(DateTimeOffset rotatedAt) => RotatedAt ??= rotatedAt;

    public void Revoke(DateTimeOffset revokedAt) => RevokedAt ??= revokedAt;
}
