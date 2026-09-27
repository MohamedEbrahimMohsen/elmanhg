using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Application.Auth.RegisterWithEmail;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.Auth.RegisterWithEmail;

public sealed class RegisterWithEmailHandlerTests
{
    private const string Email = "mona@elmanhg.test";
    private const string Password = "Password1";

    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenService<User, Guid> _refreshTokenService = Substitute.For<IRefreshTokenService<User, Guid>>();
    private readonly RegisterWithEmailHandler _handler;

    public RegisterWithEmailHandlerTests()
    {
        _tokenService.GenerateTokenAsync(Arg.Any<List<Claim>>()).Returns("access-token");
        _refreshTokenService.GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("refresh-token");
        _userManager.CreateAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Success);
        _handler = new RegisterWithEmailHandler(_userManager, _tokenService, _refreshTokenService);
    }

    [Fact]
    public async Task Handle_NewEmail_CreatesStudentWithPasswordAndReturnsTokens()
    {
        var result = await _handler.Handle(new RegisterWithEmailCommand("Mona", Email, Password), TestContext.Current.CancellationToken);

        await _userManager.Received(1).CreateAsync(Arg.Is<User>(x => x.Role == UserRole.Student && x.Email == Email), Password);
        result.User.Email.Should().Be(Email);
        result.RefreshToken.Should().Be("refresh-token");
    }

    [Fact]
    public async Task Handle_EmailAlreadyRegistered_ThrowsConflict()
    {
        _userManager.FindByEmailAsync(Email).Returns(User.CreateStudentWithEmail("Existing", Email));

        var act = () => _handler.Handle(new RegisterWithEmailCommand("Mona", Email, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.EmailAlreadyRegistered);
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_IdentityRejectsUser_ThrowsUserCreationFailed()
    {
        _userManager.CreateAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordTooShort" }));

        var act = () => _handler.Handle(new RegisterWithEmailCommand("Mona", Email, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserCreationFailed);
        await _refreshTokenService.DidNotReceive().GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
