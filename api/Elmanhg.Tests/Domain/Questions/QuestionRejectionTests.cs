using Core.Errors;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class QuestionRejectionTests
{
    private readonly QuestionBuilder _builder = new();
    private readonly Guid _editor = Guid.NewGuid();

    [Fact]
    public void Reject_AssignedTeacherOnPending_SetsRejectedReasonAndValidator()
    {
        var question = _builder.Build();

        question.Reject(Assignment(), "  Wrong unit  ");

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Rejected);
        question.RejectionReason.Should().Be("Wrong unit");
        question.ValidatedBy.Should().Be(_builder.Teacher.Id);
        question.ValidatedAt.Should().NotBeNull();
        question.UpdatedBy.Should().Be(_builder.Teacher.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_BlankReason_ThrowsQuestionRejectionReasonRequired(string reason)
    {
        var question = _builder.Build();

        var act = () => question.Reject(Assignment(), reason);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionRejectionReasonRequired);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public void Reject_AlreadyApproved_ThrowsQuestionNotPending()
    {
        var question = _builder.Approved().Build();

        var act = () => question.Reject(Assignment(), "Wrong unit");

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotPending);
    }

    [Fact]
    public void Reject_AssignmentForOtherSubject_ThrowsQuestionValidatorNotAssigned()
    {
        var question = _builder.Build();
        var assignment = TeacherSubject.Create(_builder.Teacher, Subject.Create("Chemistry", 2, Guid.NewGuid()), Guid.NewGuid());

        var act = () => question.Reject(assignment, "Wrong unit");

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionValidatorNotAssigned);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public void Resubmit_Rejected_ReturnsToPendingAndClearsRejection()
    {
        var question = _builder.Rejected("Wrong unit").Build();

        question.Resubmit(QuestionType.Mcq, QuestionBuilder.McqContent(), Metadata(), _builder.Lesson, _editor);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.RejectionReason.Should().BeNull();
        question.ValidatedBy.Should().BeNull();
        question.ValidatedAt.Should().BeNull();
        question.UpdatedBy.Should().Be(_editor);
    }

    [Fact]
    public void Resubmit_WithContentEdit_BumpsVersionAndAddsRevision()
    {
        var question = _builder.Rejected("Wrong unit").Build();

        question.Resubmit(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, Metadata(), _builder.Lesson, _editor);

        question.Version.Should().Be(2);
        question.Revisions.Should().HaveCount(2);
        question.Stem.Should().Be("<p>3 + 3 = ?</p>");
    }

    [Fact]
    public void Resubmit_WithoutChanges_KeepsVersion()
    {
        var question = _builder.Rejected("Wrong unit").Build();

        question.Resubmit(QuestionType.Mcq, QuestionBuilder.McqContent(), Metadata(), _builder.Lesson, _editor);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
        question.Version.Should().Be(1);
        question.Revisions.Should().ContainSingle();
    }

    [Fact]
    public void Resubmit_NotRejected_ThrowsQuestionNotRejected()
    {
        var question = _builder.Build();

        var act = () => question.Resubmit(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, Metadata(), _builder.Lesson, _editor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionNotRejected);
        question.Version.Should().Be(1);
    }

    [Fact]
    public void Resubmit_TypeChanged_ThrowsQuestionTypeImmutable()
    {
        var question = _builder.Rejected("Wrong unit").Build();

        var act = () => question.Resubmit(QuestionType.Multi, QuestionBuilder.McqContent(), Metadata(), _builder.Lesson, _editor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionTypeImmutable);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Rejected);
        question.RejectionReason.Should().Be("Wrong unit");
    }

    [Fact]
    public void Update_ContentEditOnRejected_StaysRejected()
    {
        var question = _builder.Rejected("Wrong unit").Build();

        question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, Metadata(), _builder.Lesson, _editor);

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Rejected);
        question.Version.Should().Be(2);
        question.RejectionReason.Should().Be("Wrong unit");
    }

    [Fact]
    public void Approve_AfterResubmit_Succeeds()
    {
        var question = _builder.Rejected("Wrong unit").Build();
        question.Resubmit(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, Metadata(), _builder.Lesson, _editor);

        question.Approve(Assignment());

        question.ValidationStatus.Should().Be(QuestionValidationStatus.Approved);
    }

    private TeacherSubject Assignment()
    {
        return TeacherSubject.Create(_builder.Teacher, _builder.Subject, Guid.NewGuid());
    }

    private static QuestionMetadata Metadata()
    {
        return new QuestionMetadata(QuestionDifficulty.Medium, null, []);
    }
}
