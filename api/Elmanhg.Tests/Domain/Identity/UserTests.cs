using Core.Errors;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Identity;

public sealed class UserTests
{
    [Fact]
    public void CreateStudentWithPhone_Always_CreatesActiveStudentWithConfirmedPhone()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");

        user.Role.Should().Be(UserRole.Student);
        user.Status.Should().Be(UserStatus.Active);
        user.UserName.Should().Be(user.PhoneNumber).And.Be("01012345678");
        user.PhoneNumberConfirmed.Should().BeTrue();
        user.DisplayName.Should().Be("Ahmed");
        user.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void CreateStudentWithEmail_Always_CreatesActiveStudentWithEmailUserName()
    {
        var user = User.CreateStudentWithEmail("Mona", "mona@elmanhg.test");

        user.Role.Should().Be(UserRole.Student);
        user.IsActive.Should().BeTrue();
        user.UserName.Should().Be(user.Email).And.Be("mona@elmanhg.test");
        user.EmailConfirmed.Should().BeFalse();
        user.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public void CreateAdmin_Always_CreatesActiveAdminWithConfirmedEmail()
    {
        var user = User.CreateAdmin("Admin", "admin@elmanhg.test");

        user.Role.Should().Be(UserRole.Admin);
        user.IsActive.Should().BeTrue();
        user.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public void CreateTeacher_Always_CreatesActiveTeacherWithConfirmedEmail()
    {
        var user = User.CreateTeacher("Teacher", "t@elmanhg.test");

        user.Role.Should().Be(UserRole.Teacher);
        user.IsActive.Should().BeTrue();
        user.UserName.Should().Be(user.Email).And.Be("t@elmanhg.test");
        user.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public void Suspend_ActiveUser_SetsSuspendedAndStampsUpdationDate()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");
        var before = DateTimeOffset.UtcNow;

        user.Suspend();

        user.Status.Should().Be(UserStatus.Suspended);
        user.IsActive.Should().BeFalse();
        user.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Suspend_AlreadySuspended_ThrowsBusinessRuleViolation()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");
        user.Suspend();

        var act = user.Suspend;

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UserAlreadySuspended);
    }
}
