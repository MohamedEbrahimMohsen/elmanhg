using Core.Errors;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.EssayGrading;

public sealed class EssayGradeReviewTests
{
    private static readonly DateTimeOffset ReviewedAt = EssayGradeBuilder.DefaultRequestedAt.AddHours(3);
    private static readonly Guid TeacherId = Guid.NewGuid();

    [Fact]
    public void Accept_LowConfidenceGrade_FinalisesWithAiScoreAndRaisesEvent()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder());
        grade.ClearDomainEvents();

        grade.Accept(TeacherId, null, ReviewedAt);

        (grade.Status, grade.ReviewDecision, grade.ReviewedScore, grade.ReviewedNormalisedScore).Should().Be((EssayGradeStatus.Graded, (GradeReviewDecision?)GradeReviewDecision.Accepted, (decimal?)2.5m, (decimal?)0.5m));
        (grade.ReviewedBy, grade.ReviewedAt, grade.UpdatedBy, grade.UpdationDate).Should().Be(((Guid?)TeacherId, (DateTimeOffset?)ReviewedAt, (Guid?)TeacherId, ReviewedAt));
        grade.ReviewReason.Should().Be(EssayReviewReason.LowConfidence);
        grade.GetDomainEvents().Should().ContainSingle().Which.Should().BeOfType<EssayGradeReviewed>().Which.Grade.Should().BeSameAs(grade);
    }

    [Fact]
    public void Accept_GradingFailedGrade_ThrowsGradeReviewNoAiScore()
    {
        var grade = EssayGradeBuilder.GradingFailed(new EssayGradeBuilder());

        var act = () => grade.Accept(TeacherId, null, ReviewedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewNoAiScore);
        grade.Status.Should().Be(EssayGradeStatus.InReview);
    }

    [Fact]
    public void Accept_PendingGrade_ThrowsGradeNotInReview()
    {
        var grade = new EssayGradeBuilder().Build();

        var act = () => grade.Accept(TeacherId, null, ReviewedAt);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.GradeNotInReview);
    }

    [Fact]
    public void Override_LowConfidenceGrade_KeepsAiScoreAndStoresTeacherScore()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder());

        grade.Override(4m, TeacherId, "  Full marks for the definition.  ", ReviewedAt);

        (grade.Score, grade.NormalisedScore).Should().Be(((decimal?)2.5m, (decimal?)0.5m));
        (grade.ReviewedScore, grade.ReviewedNormalisedScore, grade.FinalScore, grade.FinalNormalisedScore).Should().Be(((decimal?)4m, (decimal?)0.8m, (decimal?)4m, (decimal?)0.8m));
        (grade.ReviewDecision, grade.ReviewComment).Should().Be(((GradeReviewDecision?)GradeReviewDecision.Overridden, "Full marks for the definition."));
    }

    [Fact]
    public void Override_GradingFailedGrade_SetsGradedAtToReviewTime()
    {
        var grade = EssayGradeBuilder.GradingFailed(new EssayGradeBuilder());

        grade.Override(3m, TeacherId, "Good attempt.", ReviewedAt.AddTicks(7));

        (grade.Status, grade.GradedAt, grade.ReviewedAt).Should().Be((EssayGradeStatus.Graded, (DateTimeOffset?)ReviewedAt, (DateTimeOffset?)ReviewedAt));
    }

    [Fact]
    public void Override_ScoreAboveMax_ThrowsGradeReviewScoreOutOfRange()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder());

        var act = () => grade.Override(5.5m, TeacherId, "Too high.", ReviewedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewScoreOutOfRange);
        (grade.Status, grade.ReviewDecision, grade.ReviewedScore).Should().Be((EssayGradeStatus.InReview, (GradeReviewDecision?)null, (decimal?)null));
    }

    [Fact]
    public void Override_NegativeScore_ThrowsGradeReviewScoreOutOfRange()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder());

        var act = () => grade.Override(-0.5m, TeacherId, "Negative.", ReviewedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.GradeReviewScoreOutOfRange);
    }

    [Fact]
    public void Override_AlreadyReviewed_ThrowsGradeNotInReview()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder());
        grade.Accept(TeacherId, null, ReviewedAt);

        var act = () => grade.Override(4m, TeacherId, "Second decision.", ReviewedAt.AddMinutes(1));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.GradeNotInReview);
    }

    [Fact]
    public void Override_BlankComment_StoresNullComment()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder());

        grade.Override(4m, TeacherId, "   ", ReviewedAt);

        grade.ReviewComment.Should().BeNull();
    }

    [Fact]
    public void ToQuestionGrade_Overridden_ReturnsTeacherScore()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder());
        grade.Override(4m, TeacherId, "Better than the AI thought.", ReviewedAt);

        grade.ToQuestionGrade().Should().Be(new QuestionGrade(4m, 0.8m, GradeOutcome.Partial, null));
    }

    [Fact]
    public void GradedBy_Reviewed_IsTeacher()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder());
        grade.Accept(TeacherId, null, ReviewedAt);

        grade.GradedBy.Should().Be(AttemptGrader.Teacher);
    }

    [Fact]
    public void GradedBy_Unreviewed_IsAi()
    {
        var grade = new EssayGradeBuilder().Build();
        grade.Complete(EssayGradeBuilder.Assessment(0.9m), new QuestionGrade(2.5m, 0.5m, GradeOutcome.Partial, null), 0.7m, ReviewedAt);

        grade.GradedBy.Should().Be(AttemptGrader.AI);
    }

    [Fact]
    public void MarkApplied_AfterReview_StampsAppliedAt()
    {
        var grade = EssayGradeBuilder.InReview(new EssayGradeBuilder());
        grade.Override(4m, TeacherId, "Better than the AI thought.", ReviewedAt);

        grade.MarkApplied(ReviewedAt);

        (grade.AppliedAt, grade.IsAwaitingApplication).Should().Be(((DateTimeOffset?)ReviewedAt, false));
    }
}
