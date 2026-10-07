using Core.Identity.Tokens.RefreshToken;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Identity;

public sealed class IssuedRefreshTokenTests
{
    private static readonly DateTimeOffset IssuedAt = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(10);

    [Fact]
    public void Issue_Always_CreatesUnrotatedUnrevokedToken()
    {
        var userId = Guid.NewGuid();
        var familyId = Guid.NewGuid();

        var token = IssuedRefreshToken.Issue(userId, familyId, "hash", IssuedAt, IssuedAt.AddDays(7));

        (token.UserId, token.FamilyId, token.TokenHash, token.IssuedAt, token.ExpiresAt, token.RotatedAt, token.IsRevoked).Should().Be((userId, familyId, "hash", IssuedAt, IssuedAt.AddDays(7), (DateTimeOffset?)null, false));
        token.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void IsReplayed_NotRotated_ReturnsFalse()
    {
        var token = Token();

        token.IsReplayed(IssuedAt.AddDays(1), Grace).Should().BeFalse();
    }

    [Fact]
    public void IsReplayed_WithinGraceOfRotation_ReturnsFalse()
    {
        var token = Token();
        token.Rotate(IssuedAt);

        token.IsReplayed(IssuedAt.Add(Grace), Grace).Should().BeFalse();
    }

    [Fact]
    public void IsReplayed_AfterGraceOfRotation_ReturnsTrue()
    {
        var token = Token();
        token.Rotate(IssuedAt);

        token.IsReplayed(IssuedAt.Add(Grace).AddTicks(1), Grace).Should().BeTrue();
    }

    [Fact]
    public void Rotate_AlreadyRotated_KeepsFirstRotationTime()
    {
        var token = Token();
        token.Rotate(IssuedAt);

        token.Rotate(IssuedAt.AddMinutes(5));

        token.RotatedAt.Should().Be(IssuedAt);
    }

    [Fact]
    public void Revoke_AlreadyRevoked_KeepsFirstRevocationTime()
    {
        var token = Token();
        token.Revoke(IssuedAt);

        token.Revoke(IssuedAt.AddMinutes(5));

        (token.IsRevoked, token.RevokedAt).Should().Be((true, (DateTimeOffset?)IssuedAt));
    }

    private static IssuedRefreshToken Token() => IssuedRefreshToken.Issue(Guid.NewGuid(), Guid.NewGuid(), "hash", IssuedAt, IssuedAt.AddDays(7));
}
