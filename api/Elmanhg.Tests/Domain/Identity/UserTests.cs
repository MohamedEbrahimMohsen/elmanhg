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

        Action act = user.Suspend;

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UserAlreadySuspended);
    }

    [Fact]
    public void NeedsOnboarding_NewStudent_IsTrue()
    {
        var user = User.CreateStudentWithEmail("Mona", "mona@elmanhg.test");

        user.NeedsOnboarding.Should().BeTrue();
        user.OnboardedAt.Should().BeNull();
    }

    [Fact]
    public void NeedsOnboarding_Teacher_IsFalse()
    {
        var user = User.CreateTeacher("Teacher", "t@elmanhg.test");

        user.NeedsOnboarding.Should().BeFalse();
    }

    [Fact]
    public void ChooseSubjectInterests_Student_StoresDistinctIdsAndMarksOnboarded()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");
        var physics = Guid.NewGuid();
        var chemistry = Guid.NewGuid();
        var chosenAt = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        user.ChooseSubjectInterests([physics, physics, chemistry], chosenAt);

        user.SubjectInterestIds.Should().Equal(physics, chemistry);
        user.OnboardedAt.Should().Be(chosenAt);
        user.UpdationDate.Should().Be(chosenAt);
        user.NeedsOnboarding.Should().BeFalse();
    }

    [Fact]
    public void ChooseSubjectInterests_SecondCall_KeepsFirstOnboardedAtAndReplacesIds()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");
        var physics = Guid.NewGuid();
        var chemistry = Guid.NewGuid();
        var first = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
        var second = first.AddDays(3);
        user.ChooseSubjectInterests([physics], first);

        user.ChooseSubjectInterests([chemistry], second);

        user.OnboardedAt.Should().Be(first);
        user.SubjectInterestIds.Should().Equal(chemistry);
        user.UpdationDate.Should().Be(second);
    }

    [Fact]
    public void ChooseSubjectInterests_EmptyList_MarksOnboardedWithoutInterests()
    {
        var user = User.CreateStudentWithPhone("Ahmed", "01012345678");
        var chosenAt = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        user.ChooseSubjectInterests([], chosenAt);

        user.SubjectInterestIds.Should().BeEmpty();
        user.OnboardedAt.Should().Be(chosenAt);
    }

    [Fact]
    public void ChooseSubjectInterests_Teacher_ThrowsUserNotStudent()
    {
        var user = User.CreateTeacher("Teacher", "t@elmanhg.test");

        var act = () => user.ChooseSubjectInterests([Guid.NewGuid()], new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UserNotStudent);
        user.SubjectInterestIds.Should().BeEmpty();
    }

    [Fact]
    public void Suspend_ByAnotherActorWithTwoActiveAdmins_SuspendsAdminAndStampsUpdatedBy()
    {
        var admin = User.CreateAdmin("Admin", "admin@elmanhg.test");
        var actorId = Guid.NewGuid();
        var before = admin.UpdationDate;

        admin.Suspend(actorId, 2);

        admin.Status.Should().Be(UserStatus.Suspended);
        admin.UpdatedBy.Should().Be(actorId);
        admin.UpdationDate.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Suspend_ByItself_ThrowsUserCannotSuspendSelf()
    {
        var admin = User.CreateAdmin("Admin", "admin@elmanhg.test");

        var act = () => admin.Suspend(admin.Id, 5);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UserCannotSuspendSelf);
        admin.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public void Suspend_LastActiveAdmin_ThrowsLastActiveAdmin()
    {
        var admin = User.CreateAdmin("Admin", "admin@elmanhg.test");

        var act = () => admin.Suspend(Guid.NewGuid(), 1);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.LastActiveAdmin);
        admin.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Suspend_StudentWithOneActiveAdminCount_Suspends()
    {
        var student = User.CreateStudentWithPhone("Ahmed", "01012345678");

        student.Suspend(Guid.NewGuid(), 1);

        student.Status.Should().Be(UserStatus.Suspended);
    }

    [Fact]
    public void Suspend_AlreadySuspendedWithActor_ThrowsUserAlreadySuspended()
    {
        var teacher = User.CreateTeacher("Teacher", "t@elmanhg.test");
        teacher.Suspend(Guid.NewGuid(), 0);

        var act = () => teacher.Suspend(Guid.NewGuid(), 0);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UserAlreadySuspended);
    }

    [Fact]
    public void Reactivate_SuspendedUser_ActivatesAndStampsUpdatedBy()
    {
        var teacher = User.CreateTeacher("Teacher", "t@elmanhg.test");
        teacher.Suspend(Guid.NewGuid(), 0);
        var actorId = Guid.NewGuid();

        teacher.Reactivate(actorId);

        teacher.Status.Should().Be(UserStatus.Active);
        teacher.IsActive.Should().BeTrue();
        teacher.UpdatedBy.Should().Be(actorId);
    }

    [Fact]
    public void Reactivate_ActiveUser_ThrowsUserNotSuspended()
    {
        var teacher = User.CreateTeacher("Teacher", "t@elmanhg.test");

        var act = () => teacher.Reactivate(Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UserNotSuspended);
    }

    [Fact]
    public void IsInvitationPending_TeacherWithoutPassword_IsTrue()
    {
        var teacher = User.CreateTeacher("Teacher", "t@elmanhg.test");

        teacher.IsInvitationPending.Should().BeTrue();
    }

    [Fact]
    public void IsInvitationPending_TeacherWithPasswordHash_IsFalse()
    {
        var teacher = User.CreateTeacher("Teacher", "t@elmanhg.test");
        teacher.PasswordHash = "hash";

        teacher.IsInvitationPending.Should().BeFalse();
    }

    [Fact]
    public void IsInvitationPending_StudentWithoutPassword_IsFalse()
    {
        var student = User.CreateStudentWithPhone("Ahmed", "01012345678");

        student.IsInvitationPending.Should().BeFalse();
    }

    [Fact]
    public void CanBeSuspendedBy_OtherActiveUser_IsTrue()
    {
        var teacher = User.CreateTeacher("Teacher", "t@elmanhg.test");

        teacher.CanBeSuspendedBy(Guid.NewGuid(), 0).Should().BeTrue();
    }

    [Fact]
    public void CanBeSuspendedBy_Self_IsFalse()
    {
        var admin = User.CreateAdmin("Admin", "admin@elmanhg.test");

        admin.CanBeSuspendedBy(admin.Id, 5).Should().BeFalse();
    }

    [Fact]
    public void CanBeSuspendedBy_LastActiveAdmin_IsFalse()
    {
        var admin = User.CreateAdmin("Admin", "admin@elmanhg.test");

        admin.CanBeSuspendedBy(Guid.NewGuid(), 1).Should().BeFalse();
    }

    [Fact]
    public void CanBeSuspendedBy_SuspendedUser_IsFalse()
    {
        var teacher = User.CreateTeacher("Teacher", "t@elmanhg.test");
        teacher.Suspend();

        teacher.CanBeSuspendedBy(Guid.NewGuid(), 0).Should().BeFalse();
    }
}
