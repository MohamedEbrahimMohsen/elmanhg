using Core.Errors;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Teachers;

public sealed class TeacherSubjectTests
{
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());

    [Fact]
    public void Create_TeacherAndSubject_LinksBoth()
    {
        var teacher = User.CreateTeacher("Teacher", "teacher@elmanhg.test");
        var createdBy = Guid.NewGuid();

        var teacherSubject = TeacherSubject.Create(teacher, _subject, createdBy);

        teacherSubject.TeacherId.Should().Be(teacher.Id);
        teacherSubject.SubjectId.Should().Be(_subject.Id);
        teacherSubject.CreatedBy.Should().Be(createdBy);
        teacherSubject.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Create_StudentUser_ThrowsUserNotTeacher()
    {
        var student = User.CreateStudentWithEmail("Student", "student@elmanhg.test");

        var act = () => TeacherSubject.Create(student, _subject, Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UserNotTeacher);
    }

    [Fact]
    public void Create_AdminUser_ThrowsUserNotTeacher()
    {
        var admin = User.CreateAdmin("Admin", "admin@elmanhg.test");

        var act = () => TeacherSubject.Create(admin, _subject, Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UserNotTeacher);
    }

    [Fact]
    public void Unassign_Always_SoftDeletesAndStampsActor()
    {
        var teacherSubject = TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@elmanhg.test"), _subject, Guid.NewGuid());
        var unassignedBy = Guid.NewGuid();

        teacherSubject.Unassign(unassignedBy);

        teacherSubject.IsDeleted.Should().BeTrue();
        teacherSubject.UpdatedBy.Should().Be(unassignedBy);
        teacherSubject.UpdationDate.Should().BeOnOrAfter(teacherSubject.CreationDate);
    }
}
