using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Application.Auth.RefreshAccessToken;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using NSubstitute;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.Auth.RefreshAccessToken;

public sealed class RefreshAccessTokenHandlerTests
{
    private const string OldRefreshToken = "old-refresh-token";
    private const string NewRefreshToken = "new-refresh-token";

    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenService<User, Guid> _refreshTokenService = Substitute.For<IRefreshTokenService<User, Guid>>();
    private readonly RefreshAccessTokenHandler _handler;

    public RefreshAccessTokenHandlerTests()
    {
        _tokenService.GenerateTokenAsync(Arg.Any<List<Claim>>()).Returns("access-token");
        _refreshTokenService.GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns(NewRefreshToken);
        _handler = new RefreshAccessTokenHandler(_tokenService, _refreshTokenService);
    }

    [Fact]
    public async Task Handle_ValidToken_IssuesAccessTokenAndRotatesRefreshToken()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");
        _refreshTokenService.ValidateTokenAsync(OldRefreshToken, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _handler.Handle(new RefreshAccessTokenCommand(OldRefreshToken), TestContext.Current.CancellationToken);

        await _refreshTokenService.Received(1).GenerateTokenAsync(user, Arg.Any<CancellationToken>());
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be(NewRefreshToken).And.NotBe(OldRefreshToken);
    }

    [Fact]
    public async Task Handle_SuspendedUser_ThrowsForbidden()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");
        user.Suspend();
        _refreshTokenService.ValidateTokenAsync(OldRefreshToken, Arg.Any<CancellationToken>()).Returns(user);

        var act = () => _handler.Handle(new RefreshAccessTokenCommand(OldRefreshToken), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserSuspended);
        await _refreshTokenService.DidNotReceive().GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
