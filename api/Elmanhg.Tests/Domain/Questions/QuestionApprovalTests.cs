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

        question.Approve(assignment, question.Version);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
        question.ValidatedBy.Should().Be(assignment.TeacherId);
        question.ValidatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Approve_AssignmentForOtherSubject_ThrowsQuestionValidatorNotAssigned()
    {
        var question = _builder.Build();
        var assignment = TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@example.com"), Subject.Create("Chemistry", 2, Guid.NewGuid()), Guid.NewGuid());

        var act = () => question.Approve(assignment, question.Version);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionValidatorNotAssigned);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public void Approve_UnassignedAssignment_ThrowsQuestionValidatorNotAssigned()
    {
        var question = _builder.Build();
        var assignment = TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@example.com"), _builder.Subject, Guid.NewGuid());
        assignment.Unassign(Guid.NewGuid());

        var act = () => question.Approve(assignment, question.Version);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionValidatorNotAssigned);
    }

    [Fact]
    public void Approve_AlreadyApproved_ThrowsQuestionNotPending()
    {
        var question = _builder.Approved().Build();
        var assignment = TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@example.com"), _builder.Subject, Guid.NewGuid());

        var act = () => question.Approve(assignment, question.Version);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotPending);
    }

    [Fact]
    public void Approve_AssignmentForAdmin_CannotBeCreated()
    {
        var act = () => TeacherSubject.Create(User.CreateAdmin("Admin", "admin@example.com"), _builder.Subject, Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.UserNotTeacher);
    }

    [Fact]
    public void Approve_WithNewDifficulty_ChangesDifficultyAndRecordsPrevious()
    {
        var question = _builder.Build();

        question.Approve(Assignment(), question.Version, QuestionDifficulty.Hard);

        question.Difficulty.Should().Be(QuestionDifficulty.Hard);
        var decision = question.Decisions.Should().ContainSingle().Subject;
        decision.Outcome.Should().Be(QuestionDecisionOutcome.Approved);
        decision.DifficultyChangedFrom.Should().Be(QuestionDifficulty.Medium);
        decision.Difficulty.Should().Be(QuestionDifficulty.Hard);
        question.Version.Should().Be(1);
    }

    [Fact]
    public void Approve_SameDifficulty_RecordsNoDifficultyChange()
    {
        var question = _builder.Build();

        question.Approve(Assignment(), question.Version, QuestionDifficulty.Medium);

        question.Decisions.Should().ContainSingle().Which.DifficultyChangedFrom.Should().BeNull();
    }

    [Fact]
    public void Approve_StaleVersion_ThrowsQuestionVersionChanged()
    {
        var question = _builder.Build();

        var act = () => question.Approve(Assignment(), question.Version + 1);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionVersionChanged);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Decisions.Should().BeEmpty();
    }

    [Fact]
    public void Approve_Pending_AppendsDecisionWithVersionAndTeacher()
    {
        var question = _builder.Build();
        var assignment = Assignment();

        question.Approve(assignment, question.Version);

        var decision = question.Decisions.Should().ContainSingle().Subject;
        decision.Version.Should().Be(question.Version);
        decision.DecidedBy.Should().Be(assignment.TeacherId);
        decision.DecidedAt.Should().Be(question.ValidatedAt!.Value);
        decision.QuestionId.Should().Be(question.Id);
    }

    [Fact]
    public void Approve_Pending_KeepsSubmittedAtAndVersion()
    {
        var question = _builder.Build();
        var submittedAt = question.SubmittedAt;

        question.Approve(Assignment(), question.Version, QuestionDifficulty.Hard);

        question.SubmittedAt.Should().Be(submittedAt);
        question.Version.Should().Be(1);
        question.Revisions.Should().HaveCount(1);
    }

    private TeacherSubject Assignment()
    {
        return TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@example.com"), _builder.Subject, Guid.NewGuid());
    }
}
