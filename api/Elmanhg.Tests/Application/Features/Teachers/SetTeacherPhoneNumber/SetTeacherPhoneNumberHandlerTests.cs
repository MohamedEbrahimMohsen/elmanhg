using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Teachers.SetTeacherPhoneNumber;
using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Application.Features.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Teachers.SetTeacherPhoneNumber;

public sealed class SetTeacherPhoneNumberHandlerTests
{
    private const string Phone = "01012345678";

    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly User _teacher = User.CreateTeacher("Teacher", "teacher@elmanhg.test");
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly SetTeacherPhoneNumberHandler _handler;

    public SetTeacherPhoneNumberHandlerTests()
    {
        _currentUserService.UserId.Returns(_adminId);
        _userManager.FindByIdAsync(_teacher.Id.ToString()).Returns(_teacher);
        _userManager.UpdateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _handler = new SetTeacherPhoneNumberHandler(_userManager, _currentUserService);
    }

    [Fact]
    public async Task Handle_Teacher_SetsNumberAndUpdates()
    {
        await _handler.Handle(new SetTeacherPhoneNumberCommand(_teacher.Id, Phone), TestContext.Current.CancellationToken);

        (_teacher.PhoneNumber, _teacher.UpdatedBy).Should().Be((Phone, (Guid?)_adminId));
        await _userManager.Received(1).UpdateAsync(_teacher);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsUserNotFound()
    {
        var act = () => _handler.Handle(new SetTeacherPhoneNumberCommand(Guid.NewGuid(), Phone), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotFound);
        await _userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_Student_ThrowsPhoneNumberTeachersOnly()
    {
        var student = User.CreateStudentWithEmail("Student", "student@elmanhg.test");
        _userManager.FindByIdAsync(student.Id.ToString()).Returns(student);

        var act = () => _handler.Handle(new SetTeacherPhoneNumberCommand(student.Id, Phone), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.PhoneNumberTeachersOnly);
        await _userManager.DidNotReceive().UpdateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_UpdateFails_ThrowsUserModifiedConcurrently()
    {
        _userManager.UpdateAsync(Arg.Any<User>()).Returns(IdentityResult.Failed(new IdentityError { Code = "ConcurrencyFailure" }));

        var act = () => _handler.Handle(new SetTeacherPhoneNumberCommand(_teacher.Id, Phone), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserModifiedConcurrently);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new SetTeacherPhoneNumberCommand(_teacher.Id, Phone), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _userManager.DidNotReceive().FindByIdAsync(Arg.Any<string>());
    }
}
