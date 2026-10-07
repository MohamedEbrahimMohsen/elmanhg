using Core.Errors;
using Core.Identity.Exceptions;
using Core.Identity.Tokens;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Core.Identity;

public sealed class RefreshTokenRotatorTests
{
    private const string OldRefreshToken = "old-refresh-token";
    private const string NewRefreshToken = "new-refresh-token";
    private const int GraceSeconds = 10;

    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly IRefreshTokenService<User, Guid> _refreshTokenService = Substitute.For<IRefreshTokenService<User, Guid>>();
    private readonly IIssuedRefreshTokenRepository _issuedRefreshTokenRepository = Substitute.For<IIssuedRefreshTokenRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<IssuedRefreshToken> _records = [];
    private readonly User _user = User.CreateStudentWithPhone("Ahmed", "01012345678");
    private readonly RefreshTokenRotator<User> _rotator;
    private Action<IssuedRefreshToken> _addIfAbsent;

    public RefreshTokenRotatorTests()
    {
        _refreshTokenService.GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(NewRefreshToken);
        _timeProvider.GetUtcNow().Returns(Now);
        _issuedRefreshTokenRepository.FindAsync(Arg.Any<Expression<Func<IssuedRefreshToken, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<IssuedRefreshToken>, IQueryable<IssuedRefreshToken>>?>(), Arg.Any<Func<IQueryable<IssuedRefreshToken>, IOrderedQueryable<IssuedRefreshToken>>?>(), Arg.Any<bool>())
            .Returns(call => _records.Where(call.Arg<Expression<Func<IssuedRefreshToken, bool>>>().Compile()).ToList());
        _issuedRefreshTokenRepository.When(x => x.AddAsync(Arg.Any<IssuedRefreshToken>(), Arg.Any<CancellationToken>())).Do(call => _records.Add(call.Arg<IssuedRefreshToken>()));
        _addIfAbsent = _records.Add;
        _issuedRefreshTokenRepository.When(x => x.AddIfAbsentAsync(Arg.Any<IssuedRefreshToken>(), Arg.Any<CancellationToken>())).Do(call => _addIfAbsent(call.Arg<IssuedRefreshToken>()));
        _rotator = Rotator(TimeSpan.FromSeconds(GraceSeconds));
    }

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

    [Fact]
    public async Task RotateAsync_ValidToken_StoresOnlyHashes()
    {
        await _rotator.RotateAsync(_user, OldRefreshToken, TestContext.Current.CancellationToken);

        var hashes = _records.Select(x => x.TokenHash).ToList();
        hashes.Should().OnlyContain(x => x.Length == 64 && x != OldRefreshToken && x != NewRefreshToken);
        hashes.Should().BeEquivalentTo([RefreshTokenHash.Compute(OldRefreshToken), RefreshTokenHash.Compute(NewRefreshToken)]);
    }

    private RefreshTokenRotator<User> Rotator(TimeSpan reuseGrace) => new(_refreshTokenService, _issuedRefreshTokenRepository, Options.Create(new RefreshTokenRotationOptions { ReuseGrace = reuseGrace }), Options.Create(new JwtOptions { RefreshTokenExpirationDays = 7 }), _timeProvider);

    private IssuedRefreshToken Single(string token) => _records.Single(x => x.TokenHash == RefreshTokenHash.Compute(token));

    private IssuedRefreshToken Record(string token, Guid familyId, DateTimeOffset? rotatedAt)
    {
        var record = IssuedRefreshToken.Issue(_user.Id, familyId, RefreshTokenHash.Compute(token), Now.AddHours(-1), Now.AddDays(6));
        if (rotatedAt is { } at)
        {
            record.Rotate(at);
        }

        _records.Add(record);
        return record;
    }
}
