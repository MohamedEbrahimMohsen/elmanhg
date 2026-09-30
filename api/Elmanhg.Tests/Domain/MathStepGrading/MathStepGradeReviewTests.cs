using Core.Errors;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.MathStepGrading;

public sealed class MathStepGradeReviewTests
{
    private static readonly DateTimeOffset ReviewedAt = MathStepGradeBuilder.DefaultRequestedAt.AddHours(3);
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly QuestionGrade PartialGrade = new(1.5m, 0.75m, GradeOutcome.Partial, GradeFeedback.MathStepTally(1, 2));

    [Fact]
    public void Accept_LowConfidenceGrade_FinalisesWithAiScore()
    {
        var grade = LowConfidence();

        grade.Accept(TeacherId, "Fine.", ReviewedAt);

        (grade.Status, grade.ReviewDecision, grade.ReviewedScore, grade.ReviewedNormalisedScore).Should().Be((MathStepGradeStatus.Graded, (GradeReviewDecision?)GradeReviewDecision.Accepted, (decimal?)1.5m, (decimal?)0.75m));
        (grade.ReviewedBy, grade.ReviewedAt, grade.ReviewComment).Should().Be(((Guid?)TeacherId, (DateTimeOffset?)ReviewedAt, "Fine."));
        grade.GetDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void Accept_FinalAnswerUnchecked_ThrowsGradeReviewNoAiScore()
    {
        var grade = Unchecked();

        var act = () => grade.Accept(TeacherId, null, ReviewedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewNoAiScore);
    }

    [Fact]
    public void Override_FinalAnswerUnchecked_StoresScoreAndGradedAt()
    {
        var grade = Unchecked();

        grade.Override(2m, TeacherId, "Correct method.", ReviewedAt);

        (grade.Status, grade.ReviewedScore, grade.ReviewedNormalisedScore, grade.GradedAt).Should().Be((MathStepGradeStatus.Graded, (decimal?)2m, (decimal?)1m, (DateTimeOffset?)ReviewedAt));
        (grade.Score, grade.FinalAnswerVerdict, grade.ReviewReason).Should().Be(((decimal?)null, (MathAnswerVerdict?)null, (MathStepReviewReason?)MathStepReviewReason.FinalAnswerUnchecked));
    }

    [Fact]
    public void Override_ScoreAboveMax_ThrowsGradeReviewScoreOutOfRange()
    {
        var grade = Unchecked();

        var act = () => grade.Override(2.5m, TeacherId, "Too high.", ReviewedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewScoreOutOfRange);
    }

    [Fact]
    public void Accept_PendingGrade_ThrowsGradeNotInReview()
    {
        var grade = new MathStepGradeBuilder().Build();

        var act = () => grade.Accept(TeacherId, null, ReviewedAt);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.GradeNotInReview);
    }

    [Fact]
    public void ToQuestionGrade_Overridden_ReturnsTeacherScoreWithoutFeedback()
    {
        var grade = LowConfidence();
        grade.Override(2m, TeacherId, "Both steps are right.", ReviewedAt);

        grade.ToQuestionGrade().Should().Be(new QuestionGrade(2m, 1m, GradeOutcome.Correct, null));
    }

    [Fact]
    public void ToQuestionGrade_Accepted_KeepsStoredFeedback()
    {
        var grade = LowConfidence();
        grade.Accept(TeacherId, null, ReviewedAt);

        grade.ToQuestionGrade().Should().Be(new QuestionGrade(1.5m, 0.75m, GradeOutcome.Partial, GradeFeedback.MathStepTally(1, 2)));
    }

    [Fact]
    public void GradedBy_Reviewed_IsTeacher()
    {
        var grade = Unchecked();
        grade.Override(1m, TeacherId, "Half right.", ReviewedAt);

        grade.GradedBy.Should().Be(AttemptGrader.Teacher);
    }

    private static MathStepGrade LowConfidence()
    {
        var grade = new MathStepGradeBuilder().Build();
        grade.Complete(MathStepGradeBuilder.Assessment(0.5m), PartialGrade, 0.7m, MathStepGradeBuilder.DefaultRequestedAt.AddSeconds(40));
        return grade;
    }

    private static MathStepGrade Unchecked()
    {
        var grade = new MathStepGradeBuilder().WithVerdict(null).Build();
        grade.FailAttempt("MATH_CHECK_UNAVAILABLE", MathStepGradeBuilder.DefaultRequestedAt.AddSeconds(5), 1, TimeSpan.FromSeconds(30));
        return grade;
    }
}
