using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Application.Auth.LoginWithEmail;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.Auth.LoginWithEmail;

public sealed class LoginWithEmailHandlerTests
{
    private const string Email = "admin@elmanhg.test";
    private const string Password = "Password1";

    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenService<User, Guid> _refreshTokenService = Substitute.For<IRefreshTokenService<User, Guid>>();
    private readonly LoginWithEmailHandler _handler;

    public LoginWithEmailHandlerTests()
    {
        _tokenService.GenerateTokenAsync(Arg.Any<List<Claim>>()).Returns("access-token");
        _refreshTokenService.GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("refresh-token");
        _handler = new LoginWithEmailHandler(_userManager, _tokenService, _refreshTokenService);
    }

    [Fact]
    public async Task Handle_ValidCredentials_ResetsFailedCountAndReturnsTokens()
    {
        var user = ArrangeUser(User.CreateAdmin("Admin", Email), passwordMatches: true);

        var result = await _handler.Handle(new LoginWithEmailCommand(Email, Password), TestContext.Current.CancellationToken);

        await _userManager.Received(1).ResetAccessFailedCountAsync(user);
        result.User.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Handle_UnknownEmail_ThrowsInvalidLogin()
    {
        var act = () => _handler.Handle(new LoginWithEmailCommand(Email, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserInvalidLogin);
        await _userManager.DidNotReceive().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_LockedOut_ThrowsUserLockedOut()
    {
        var user = ArrangeUser(User.CreateAdmin("Admin", Email), passwordMatches: true);
        _userManager.IsLockedOutAsync(user).Returns(true);

        var act = () => _handler.Handle(new LoginWithEmailCommand(Email, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<RateLimitExceededCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserLockedOut);
        await _userManager.DidNotReceive().CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_WrongPassword_RecordsFailureAndThrowsInvalidLogin()
    {
        var user = ArrangeUser(User.CreateAdmin("Admin", Email), passwordMatches: false);

        var act = () => _handler.Handle(new LoginWithEmailCommand(Email, "Wrong1234"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserInvalidLogin);
        await _userManager.Received(1).AccessFailedAsync(user);
        await _userManager.DidNotReceive().ResetAccessFailedCountAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_SuspendedUser_ThrowsForbidden()
    {
        var suspended = User.CreateStudentWithEmail("Mona", Email);
        suspended.Suspend();
        ArrangeUser(suspended, passwordMatches: true);

        var act = () => _handler.Handle(new LoginWithEmailCommand(Email, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserSuspended);
        await _userManager.DidNotReceive().ResetAccessFailedCountAsync(Arg.Any<User>());
    }

    private User ArrangeUser(User user, bool passwordMatches)
    {
        _userManager.FindByEmailAsync(Email).Returns(user);
        _userManager.IsLockedOutAsync(user).Returns(false);
        _userManager.CheckPasswordAsync(user, Arg.Any<string>()).Returns(passwordMatches);
        return user;
    }
}
