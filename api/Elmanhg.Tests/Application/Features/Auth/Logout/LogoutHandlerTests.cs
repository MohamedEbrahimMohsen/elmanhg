using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Auth.Logout;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Auth.Logout;

public sealed class LogoutHandlerTests
{
    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly LogoutHandler _handler;

    public LogoutHandlerTests()
    {
        _handler = new LogoutHandler(_userManager, _currentUserService);
    }

    [Fact]
    public async Task Handle_AuthenticatedUser_RotatesSecurityStamp()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");
        _currentUserService.UserId.Returns(user.Id);
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        await _handler.Handle(new LogoutCommand(), TestContext.Current.CancellationToken);

        await _userManager.Received(1).UpdateSecurityStampAsync(user);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new LogoutCommand(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _userManager.DidNotReceive().UpdateSecurityStampAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_UserMissing_ThrowsUnauthorizedUserNotFound()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new LogoutCommand(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotFound);
        await _userManager.DidNotReceive().UpdateSecurityStampAsync(Arg.Any<User>());
    }
}
