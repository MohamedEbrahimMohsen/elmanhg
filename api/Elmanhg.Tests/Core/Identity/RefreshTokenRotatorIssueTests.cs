using Core.Identity.Tokens.RefreshToken;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Core.Identity;

public sealed class RefreshTokenRotatorIssueTests : RefreshTokenRotatorTestBase
{
    [Fact]
    public async Task RotateAsync_ValidToken_ReturnsNewTokenAndSavesOnce()
    {
        var result = await _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        result.Should().Be(NewRefreshToken);
        await _refreshTokenService.Received(1).GenerateTokenAsync(_user, Arg.Any<CancellationToken>());
        await _issuedRefreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateAsync_SignInToken_RecordsItRotatedAndTheNewTokenInOneFamily()
    {
        await _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        var presented = Single(OldRefreshToken);
        var issued = Single(NewRefreshToken);
        (presented.RotatedAt, issued.RotatedAt, issued.FamilyId, issued.UserId, issued.IssuedAt, issued.ExpiresAt).Should().Be((Now, (DateTimeOffset?)null, presented.FamilyId, _user.Id, Now, Now.AddDays(7)));
        await _issuedRefreshTokenRepository.Received(1).AddIfAbsentAsync(Arg.Any<IssuedRefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateAsync_SignInTokenRecordedByConcurrentRefresh_IssuesTokenInTheWinningFamily()
    {
        var winningFamilyId = Guid.NewGuid();
        _addIfAbsent = _ => Record(OldRefreshToken, winningFamilyId, rotatedAt: null);

        await _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        var presented = Single(OldRefreshToken);
        (presented.FamilyId, presented.RotatedAt, Single(NewRefreshToken).FamilyId).Should().Be((winningFamilyId, (DateTimeOffset?)Now, winningFamilyId));
        await _issuedRefreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateAsync_RecordedToken_DoesNotInsertItAgain()
    {
        var familyId = Guid.NewGuid();
        var recorded = Record(OldRefreshToken, familyId, rotatedAt: null);

        await _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        await _issuedRefreshTokenRepository.DidNotReceive().AddIfAbsentAsync(Arg.Any<IssuedRefreshToken>(), Arg.Any<CancellationToken>());
        (Single(NewRefreshToken).FamilyId, recorded.RotatedAt).Should().Be((familyId, (DateTimeOffset?)Now));
    }

    [Fact]
    public async Task RotateAsync_ValidToken_StoresOnlyHashes()
    {
        await _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        var hashes = _records.Select(x => x.TokenHash).ToList();
        hashes.Should().OnlyContain(x => x.Length == 64 && x != OldRefreshToken && x != NewRefreshToken);
        hashes.Should().BeEquivalentTo([RefreshTokenHash.Compute(OldRefreshToken), RefreshTokenHash.Compute(NewRefreshToken)]);
    }
}
