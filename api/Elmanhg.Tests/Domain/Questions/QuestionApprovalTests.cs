using Core.Errors;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class QuestionApprovalTests
{
    private readonly QuestionBuilder _builder = new();

    [Fact]
    public void Approve_AssignedTeacherOnPending_SetsApprovedAndValidator()
    {
        var question = _builder.Build();
        var assignment = TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@example.com"), _builder.Subject, Guid.NewGuid());

        question.Approve(assignment);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
        question.ValidatedBy.Should().Be(assignment.TeacherId);
        question.ValidatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_AssignmentForOtherSubject_ThrowsQuestionValidatorNotAssigned()
    {
        var question = _builder.Build();
        var assignment = TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@example.com"), Subject.Create("Chemistry", 2, Guid.NewGuid()), Guid.NewGuid());

        var act = () => question.Approve(assignment);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionValidatorNotAssigned);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public void Approve_UnassignedAssignment_ThrowsQuestionValidatorNotAssigned()
    {
        var question = _builder.Build();
        var assignment = TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@example.com"), _builder.Subject, Guid.NewGuid());
        assignment.Unassign(Guid.NewGuid());

        var act = () => question.Approve(assignment);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionValidatorNotAssigned);
    }

    [Fact]
    public void Approve_AlreadyApproved_ThrowsQuestionNotPending()
    {
        var question = _builder.Approved().Build();
        var assignment = TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@example.com"), _builder.Subject, Guid.NewGuid());

        var act = () => question.Approve(assignment);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotPending);
    }

    [Fact]
    public void Approve_AssignmentForAdmin_CannotBeCreated()
    {
        var act = () => TeacherSubject.Create(User.CreateAdmin("Admin", "admin@example.com"), _builder.Subject, Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UserNotTeacher);
    }
}
