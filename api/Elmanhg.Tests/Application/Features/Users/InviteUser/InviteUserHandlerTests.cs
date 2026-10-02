using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Email;
using Elmanhg.Application.Users.InviteUser;
using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Application.Features.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Users.InviteUser;

public sealed class InviteUserHandlerTests
{
    private const string Email = "teacher@elmanhg.test";

    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly IInvitationEmailSender _invitationEmailSender = Substitute.For<IInvitationEmailSender>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly InviteUserHandler _handler;

    public InviteUserHandlerTests()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _invitationEmailSender.SendAsync(Arg.Any<InvitationEmail>(), Arg.Any<CancellationToken>()).Returns(true);
        _handler = new InviteUserHandler(_userManager, _invitationEmailSender, _currentUserService);
    }

    [Fact]
    public async Task Handle_Teacher_CreatesTeacherWithoutPassword()
    {
        var result = await _handler.Handle(new InviteUserCommand(UserRole.Teacher, " Teacher ", $" {Email} "), TestContext.Current.CancellationToken);

        await _userManager.Received(1).CreateAsync(Arg.Is<User>(x => x.Role == UserRole.Teacher && x.Email == Email && x.DisplayName == "Teacher" && x.Id == result.UserId && x.PasswordHash == null));
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<User>(), Arg.Any<string>());
        await _invitationEmailSender.Received(1).SendAsync(new InvitationEmail(Email, "Teacher", UserRole.Teacher), Arg.Any<CancellationToken>());
        result.EmailSent.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Admin_CreatesAdmin()
    {
        _invitationEmailSender.SendAsync(Arg.Any<InvitationEmail>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new InviteUserCommand(UserRole.Admin, "Admin", Email), TestContext.Current.CancellationToken);

        await _userManager.Received(1).CreateAsync(Arg.Is<User>(x => x.Role == UserRole.Admin && x.Id == result.UserId));
        result.EmailSent.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_EmailRegistered_ThrowsConflict()
    {
        _userManager.FindByEmailAsync(Email).Returns(User.CreateStudentWithEmail("Student", Email));

        var act = () => _handler.Handle(new InviteUserCommand(UserRole.Teacher, "Teacher", Email), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.EmailAlreadyRegistered);
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<User>());
        await _invitationEmailSender.DidNotReceive().SendAsync(Arg.Any<InvitationEmail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CreateFails_ThrowsUserCreationFailed()
    {
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName" }));

        var act = () => _handler.Handle(new InviteUserCommand(UserRole.Teacher, "Teacher", Email), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserCreationFailed);
        await _invitationEmailSender.DidNotReceive().SendAsync(Arg.Any<InvitationEmail>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new InviteUserCommand(UserRole.Teacher, "Teacher", Email), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_TeacherWithPhone_CreatesTeacherWithUnconfirmedPhone()
    {
        var result = await _handler.Handle(new InviteUserCommand(UserRole.Teacher, "Teacher", Email, "01012345678"), TestContext.Current.CancellationToken);

        await _userManager.Received(1).CreateAsync(Arg.Is<User>(x => x.Id == result.UserId && x.PhoneNumber == "01012345678" && !x.PhoneNumberConfirmed && x.UserName == Email));
    }
}
