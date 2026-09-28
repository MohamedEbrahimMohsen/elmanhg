using Core.Errors;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Questions;

public sealed class QuestionRetirementTests
{
    private readonly QuestionBuilder _builder = new();
    private readonly Guid _actor = Guid.NewGuid();

    [Fact]
    public void Retire_ActiveQuestion_StampsRetiredAtAndRaisesQuestionRetired()
    {
        var question = _builder.Approved().Build();

        question.Retire(_actor);

        question.RetiredAt.Should().NotBeNull();
        question.IsRetired.Should().BeTrue();
        question.UpdatedBy.Should().Be(_actor);
        question.GetDomainEvents().Should().EndWith(new QuestionRetired(question.Id, question.LessonId));
    }

    [Theory]
    [InlineData(QuestionValidationStatus.Pending)]
    [InlineData(QuestionValidationStatus.Approved)]
    [InlineData(QuestionValidationStatus.Rejected)]
    public void Retire_AnyValidationStatus_KeepsStatusAndVersion(QuestionValidationStatus status)
    {
        var builder = status switch
        {
            QuestionValidationStatus.Approved => _builder.Approved(),
            QuestionValidationStatus.Rejected => _builder.Rejected("Wrong unit"),
            _ => _builder,
        };
        var question = builder.Build();

        question.Retire(_actor);

        question.ValidationStatus.Should().Be(status);
        question.Version.Should().Be(1);
        question.Revisions.Should().ContainSingle();
    }

    [Fact]
    public void Retire_AlreadyRetired_ThrowsQuestionAlreadyRetired()
    {
        var question = _builder.Retired().Build();
        var retiredAt = question.RetiredAt;

        var act = () => question.Retire(_actor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionAlreadyRetired);
        question.RetiredAt.Should().Be(retiredAt);
    }

    [Fact]
    public void Update_RetiredQuestion_ThrowsQuestionRetired()
    {
        var question = _builder.Retired().Build();

        var act = () => question.Update(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, Metadata(), _builder.Lesson, _actor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionRetired);
        question.Stem.Should().Be("<p>2 + 2 = ?</p>");
        question.Version.Should().Be(1);
    }

    [Fact]
    public void Resubmit_RetiredRejectedQuestion_ThrowsQuestionRetired()
    {
        var question = _builder.Rejected("x").Retired().Build();

        var act = () => question.Resubmit(QuestionType.Mcq, QuestionBuilder.McqContent() with { Stem = "<p>3 + 3 = ?</p>" }, Metadata(), _builder.Lesson, _actor);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionRetired);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Rejected);
    }

    [Fact]
    public void Approve_RetiredPendingQuestion_ThrowsQuestionRetired()
    {
        var question = _builder.Retired().Build();

        var act = () => question.Approve(Assignment(), question.Version);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionRetired);
        question.ValidationStatus.Should().Be(QuestionValidationStatus.Pending);
    }

    [Fact]
    public void Reject_RetiredPendingQuestion_ThrowsQuestionRetired()
    {
        var question = _builder.Retired().Build();

        var act = () => question.Reject(Assignment(), question.Version, "Wrong unit");

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionRetired);
        question.RejectionReason.Should().BeNull();
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
