using Core.Errors;
using Core.Identity.Exceptions;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Core.Identity;

public sealed class RefreshTokenRotatorReplayTests : RefreshTokenRotatorTestBase
{
    [Fact]
    public async Task RotateAsync_RotatedTokenReplayedAfterGrace_RevokesFamilyAndThrowsRefreshTokenRevoked()
    {
        var familyId = Guid.NewGuid();
        var replayed = Record(OldRefreshToken, familyId, rotatedAt: Now.AddSeconds(-GraceSeconds - 1));
        var successor = Record("successor-token", familyId, rotatedAt: null);
        var otherSignIn = Record("other-sign-in-token", Guid.NewGuid(), rotatedAt: null);

        var act = () => _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RefreshTokenRevoked);
        (replayed.IsRevoked, successor.IsRevoked, otherSignIn.IsRevoked, replayed.RevokedAt).Should().Be((true, true, false, (DateTimeOffset?)Now));
        await _issuedRefreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _refreshTokenService.DidNotReceive().GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        _records.Should().NotContain(x => x.TokenHash == RefreshTokenHash.Compute(NewRefreshToken));
    }

    [Fact]
    public async Task RotateAsync_ReplayWithEarlierRevokedSibling_KeepsItsFirstRevocationTime()
    {
        var familyId = Guid.NewGuid();
        var sibling = Record("sibling-token", familyId, rotatedAt: null);
        sibling.Revoke(Now.AddHours(-1));
        Record(OldRefreshToken, familyId, rotatedAt: Now.AddSeconds(-GraceSeconds - 1));

        var act = () => _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RefreshTokenRevoked);
        sibling.RevokedAt.Should().Be(Now.AddHours(-1));
    }

    [Fact]
    public async Task RotateAsync_RotatedTokenReplayedAtGraceBoundary_IssuesTokenInSameFamily()
    {
        var familyId = Guid.NewGuid();
        var replayed = Record(OldRefreshToken, familyId, rotatedAt: Now.AddSeconds(-GraceSeconds));

        var result = await _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        (result, Single(NewRefreshToken).FamilyId, replayed.RotatedAt).Should().Be((NewRefreshToken, familyId, (DateTimeOffset?)Now.AddSeconds(-GraceSeconds)));
        _records.Should().NotContain(x => x.IsRevoked);
    }

    [Fact]
    public async Task RotateAsync_ReplayWithinLongerConfiguredGrace_IssuesToken()
    {
        Record(OldRefreshToken, Guid.NewGuid(), rotatedAt: Now.AddSeconds(-20));

        var result = await Rotator(TimeSpan.FromSeconds(30)).RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        result.Should().Be(NewRefreshToken);
        _records.Should().NotContain(x => x.IsRevoked);
    }

    [Fact]
    public async Task RotateAsync_RevokedToken_ThrowsRefreshTokenRevokedWithoutSaving()
    {
        Record(OldRefreshToken, Guid.NewGuid(), rotatedAt: null).Revoke(Now.AddMinutes(-1));

        var act = () => _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RefreshTokenRevoked);
        await _issuedRefreshTokenRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _refreshTokenService.DidNotReceive().GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateAsync_RevokedTokenAlsoReplayed_ThrowsWithoutRevokingSiblingsOrSaving()
    {
        var familyId = Guid.NewGuid();
        Record(OldRefreshToken, familyId, rotatedAt: Now.AddHours(-1)).Revoke(Now.AddMinutes(-30));
        var sibling = Record("sibling-token", familyId, rotatedAt: null);

        var act = () => _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RefreshTokenRevoked);
        sibling.IsRevoked.Should().BeFalse();
        await _issuedRefreshTokenRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
