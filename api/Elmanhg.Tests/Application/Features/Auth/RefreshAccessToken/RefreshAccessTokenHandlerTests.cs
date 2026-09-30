using Core.Errors;
using Core.Identity.Tokens;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Application.Auth.RefreshAccessToken;
using Elmanhg.Application.Auth.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.Auth.RefreshAccessToken;

public sealed class RefreshAccessTokenHandlerTests
{
    private const string OldRefreshToken = "old-refresh-token";
    private const string NewRefreshToken = "new-refresh-token";
    private const int GraceSeconds = 10;

    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenService<User, Guid> _refreshTokenService = Substitute.For<IRefreshTokenService<User, Guid>>();
    private readonly IIssuedRefreshTokenRepository _issuedRefreshTokenRepository = Substitute.For<IIssuedRefreshTokenRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<IssuedRefreshToken> _records = [];
    private readonly User _user = User.CreateStudentWithPhone("Ahmed", "01012345678");
    private readonly RefreshAccessTokenHandler _handler;

    public RefreshAccessTokenHandlerTests()
    {
        _tokenService.GenerateTokenAsync(Arg.Any<List<Claim>>()).Returns("access-token");
        _refreshTokenService.GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(NewRefreshToken);
        _refreshTokenService.ValidateTokenAsync(OldRefreshToken, Arg.Any<CancellationToken>()).Returns(_user);
        _timeProvider.GetUtcNow().Returns(Now);
        _issuedRefreshTokenRepository.FindAsync(Arg.Any<Expression<Func<IssuedRefreshToken, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<IssuedRefreshToken>, IQueryable<IssuedRefreshToken>>?>(), Arg.Any<Func<IQueryable<IssuedRefreshToken>, IOrderedQueryable<IssuedRefreshToken>>?>(), Arg.Any<bool>())
            .Returns(call => _records.Where(call.Arg<Expression<Func<IssuedRefreshToken, bool>>>().Compile()).ToList());
        _issuedRefreshTokenRepository.When(x => x.AddAsync(Arg.Any<IssuedRefreshToken>(), Arg.Any<CancellationToken>())).Do(call => _records.Add(call.Arg<IssuedRefreshToken>()));
        var authOptions = Options.Create(new AuthOptions { RefreshTokenReuseGraceSeconds = GraceSeconds });
        var jwtOptions = Options.Create(new JwtOptions { RefreshTokenExpirationDays = 7 });
        _handler = new RefreshAccessTokenHandler(_tokenService, _refreshTokenService, _issuedRefreshTokenRepository, authOptions, jwtOptions, _timeProvider);
    }

    [Fact]
    public async Task Handle_ValidToken_IssuesAccessTokenAndRotatesRefreshToken()
    {
        var result = await _handler.Handle(new RefreshAccessTokenCommand(OldRefreshToken), TestContext.Current.CancellationToken);

        await _refreshTokenService.Received(1).GenerateTokenAsync(_user, Arg.Any<CancellationToken>());
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be(NewRefreshToken).And.NotBe(OldRefreshToken);
        await _issuedRefreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SignInToken_RecordsItRotatedAndTheNewTokenInOneFamily()
    {
        await _handler.Handle(new RefreshAccessTokenCommand(OldRefreshToken), TestContext.Current.CancellationToken);

        var presented = _records.Single(x => x.TokenHash == RefreshTokenHash.Compute(OldRefreshToken));
        var issued = _records.Single(x => x.TokenHash == RefreshTokenHash.Compute(NewRefreshToken));
        (presented.RotatedAt, issued.RotatedAt, issued.FamilyId, issued.UserId, issued.ExpiresAt).Should().Be((Now, (DateTimeOffset?)null, presented.FamilyId, _user.Id, Now.AddDays(7)));
    }

    [Fact]
    public async Task Handle_RotatedTokenReplayedAfterGrace_RevokesFamilyAndThrowsRefreshTokenRevoked()
    {
        var familyId = Guid.NewGuid();
        var replayed = Record(OldRefreshToken, familyId, rotatedAt: Now.AddSeconds(-GraceSeconds - 1));
        var successor = Record("successor-token", familyId, rotatedAt: null);
        var otherSignIn = Record("other-sign-in-token", Guid.NewGuid(), rotatedAt: null);

        var act = () => _handler.Handle(new RefreshAccessTokenCommand(OldRefreshToken), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RefreshTokenRevoked);
        (replayed.IsRevoked, successor.IsRevoked, otherSignIn.IsRevoked).Should().Be((true, true, false));
        await _issuedRefreshTokenRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _refreshTokenService.DidNotReceive().GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RotatedTokenReplayedWithinGrace_IssuesTokenInSameFamily()
    {
        var familyId = Guid.NewGuid();
        Record(OldRefreshToken, familyId, rotatedAt: Now.AddSeconds(-GraceSeconds));

        var result = await _handler.Handle(new RefreshAccessTokenCommand(OldRefreshToken), TestContext.Current.CancellationToken);

        result.RefreshToken.Should().Be(NewRefreshToken);
        _records.Single(x => x.TokenHash == RefreshTokenHash.Compute(NewRefreshToken)).FamilyId.Should().Be(familyId);
        _records.Should().NotContain(x => x.IsRevoked);
    }

    [Fact]
    public async Task Handle_RevokedFamilyToken_ThrowsRefreshTokenRevoked()
    {
        Record(OldRefreshToken, Guid.NewGuid(), rotatedAt: null).Revoke(Now.AddMinutes(-1));

        var act = () => _handler.Handle(new RefreshAccessTokenCommand(OldRefreshToken), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RefreshTokenRevoked);
        await _issuedRefreshTokenRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuspendedUser_ThrowsForbidden()
    {
        _user.Suspend();

        var act = () => _handler.Handle(new RefreshAccessTokenCommand(OldRefreshToken), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserSuspended);
        await _refreshTokenService.DidNotReceive().GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _issuedRefreshTokenRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

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
