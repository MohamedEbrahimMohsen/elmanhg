using Core.Errors;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.EssayGrading;

public sealed class EssayGradeTests
{
    private static readonly DateTimeOffset RequestedAt = EssayGradeBuilder.DefaultRequestedAt;
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(30);
    private static readonly QuestionGrade PartialGrade = new(2.5m, 0.5m, GradeOutcome.Partial, null);

    [Fact]
    public void Request_NewGrade_IsPendingAndDueAtRequest()
    {
        var grade = new EssayGradeBuilder().WithAnswer("  نص المقال  ").RequestedAt(RequestedAt.AddTicks(7)).Build();

        (grade.Status, grade.Attempts, grade.RequestedAt, grade.NextAttemptAt).Should().Be((EssayGradeStatus.Pending, 0, RequestedAt, (DateTimeOffset?)RequestedAt));
        grade.IsDueAt(grade.RequestedAt).Should().BeTrue();
        grade.ReadAnswerText().Should().Be("نص المقال");
    }

    [Fact]
    public void Request_BlankAnswer_ThrowsInvalidOperationException()
    {
        var act = () => new EssayGradeBuilder().WithAnswer("  ").Build();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IsDueAt_BeforeNextAttempt_ReturnsFalse()
    {
        var grade = new EssayGradeBuilder().Build();
        grade.FailAttempt("ESSAY_GRADING_UNAVAILABLE", RequestedAt, 4, RetryBaseDelay);

        grade.IsDueAt(RequestedAt.AddSeconds(1)).Should().BeFalse();
    }

    [Fact]
    public void Complete_ConfidentAssessment_StoresResultAndMarksGraded()
    {
        var grade = new EssayGradeBuilder().Build();
        var assessment = EssayGradeBuilder.Assessment(0.9m);
        var gradedAt = RequestedAt.AddSeconds(40);

        grade.Complete(assessment, PartialGrade, 0.7m, gradedAt.AddTicks(3));

        (grade.Status, grade.ReviewReason, grade.Score, grade.NormalisedScore).Should().Be((EssayGradeStatus.Graded, (EssayReviewReason?)null, (decimal?)2.5m, (decimal?)0.5m));
        grade.ReadCriteria().Should().Equal(assessment.Criteria);
        (grade.Justification, grade.Confidence, grade.Model, grade.PromptVersion).Should().Be(("جيد", (decimal?)0.9m, "claude-sonnet-5", "v1"));
        (grade.InputTokens, grade.OutputTokens, grade.CostUsd).Should().Be(((int?)900, (int?)150, (decimal?)0.00495m));
        (grade.Attempts, grade.NextAttemptAt, grade.GradedAt, grade.UpdationDate).Should().Be((1, (DateTimeOffset?)null, (DateTimeOffset?)gradedAt, gradedAt));
    }

    [Fact]
    public void Complete_LowConfidence_MarksInReviewForLowConfidence()
    {
        var grade = new EssayGradeBuilder().Build();

        grade.Complete(EssayGradeBuilder.Assessment(0.5m), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        (grade.Status, grade.ReviewReason, grade.Score).Should().Be((EssayGradeStatus.InReview, (EssayReviewReason?)EssayReviewReason.LowConfidence, (decimal?)2.5m));
    }

    [Fact]
    public void Complete_ConfidenceAtThreshold_MarksGraded()
    {
        var grade = new EssayGradeBuilder().Build();

        grade.Complete(EssayGradeBuilder.Assessment(0.7m), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        grade.Status.Should().Be(EssayGradeStatus.Graded);
    }

    [Fact]
    public void Complete_NotPending_ThrowsConflictEssayGradeNotPending()
    {
        var grade = new EssayGradeBuilder().Build();
        grade.Complete(EssayGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        var act = () => grade.Complete(EssayGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(50));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.EssayGradeNotPending);
    }

    [Fact]
    public void FailAttempt_BelowMax_SchedulesExponentialRetry()
    {
        var grade = new EssayGradeBuilder().Build();
        var firstFailure = RequestedAt.AddSeconds(5);
        var secondFailure = RequestedAt.AddSeconds(50);

        grade.FailAttempt("ESSAY_GRADING_UNAVAILABLE", firstFailure, 4, RetryBaseDelay);
        var firstRetry = grade.NextAttemptAt;
        grade.FailAttempt("ESSAY_GRADING_UNAVAILABLE", secondFailure, 4, RetryBaseDelay);

        firstRetry.Should().Be(firstFailure.AddSeconds(30));
        (grade.NextAttemptAt, grade.Attempts, grade.LastErrorCode, grade.Status).Should().Be(((DateTimeOffset?)secondFailure.AddSeconds(60), 2, "ESSAY_GRADING_UNAVAILABLE", EssayGradeStatus.Pending));
    }

    [Fact]
    public void FailAttempt_ReachesMax_MarksInReviewForGradingFailed()
    {
        var grade = new EssayGradeBuilder().Build();

        grade.FailAttempt("ESSAY_GRADING_UNAVAILABLE", RequestedAt.AddSeconds(5), 1, RetryBaseDelay);

        (grade.Status, grade.ReviewReason, grade.NextAttemptAt).Should().Be((EssayGradeStatus.InReview, (EssayReviewReason?)EssayReviewReason.GradingFailed, (DateTimeOffset?)null));
    }

    [Fact]
    public void FailAttempt_LongErrorCode_TruncatesTo100()
    {
        var grade = new EssayGradeBuilder().Build();

        grade.FailAttempt(new string('X', 150), RequestedAt.AddSeconds(5), 4, RetryBaseDelay);

        grade.LastErrorCode.Should().HaveLength(100);
    }

    [Fact]
    public void FailAttempt_NotPending_ThrowsConflictEssayGradeNotPending()
    {
        var grade = new EssayGradeBuilder().Build();
        grade.Complete(EssayGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        var act = () => grade.FailAttempt("ESSAY_GRADING_UNAVAILABLE", RequestedAt.AddSeconds(50), 4, RetryBaseDelay);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.EssayGradeNotPending);
    }

    [Fact]
    public void Request_StoresTimeTaken()
    {
        var grade = new EssayGradeBuilder().WithTimeTaken(42_000).Build();

        grade.TimeTakenMilliseconds.Should().Be(42_000);
    }

    [Fact]
    public void Request_NegativeTimeTaken_ThrowsArgumentOutOfRange()
    {
        var act = () => new EssayGradeBuilder().WithTimeTaken(-1).Build();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ToQuestionGrade_Graded_ReturnsScoreNormalisedAndOutcome()
    {
        var grade = new EssayGradeBuilder().Build();
        grade.Complete(EssayGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        grade.ToQuestionGrade().Should().Be(new QuestionGrade(2.5m, 0.5m, GradeOutcome.Partial, null));
    }

    [Fact]
    public void ToQuestionGrade_Pending_ThrowsInvalidOperation()
    {
        var grade = new EssayGradeBuilder().Build();

        var act = () => grade.ToQuestionGrade();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkApplied_Graded_StampsAppliedAtAndStopsAwaiting()
    {
        var grade = new EssayGradeBuilder().Build();
        grade.Complete(EssayGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));
        var awaitingBefore = grade.IsAwaitingApplication;

        grade.MarkApplied(RequestedAt.AddSeconds(41).AddTicks(3));

        (awaitingBefore, grade.IsAwaitingApplication, grade.AppliedAt).Should().Be((true, false, (DateTimeOffset?)RequestedAt.AddSeconds(41)));
    }

    [Fact]
    public void MarkApplied_InReview_ThrowsInvalidOperation()
    {
        var grade = new EssayGradeBuilder().Build();
        grade.Complete(EssayGradeBuilder.Assessment(0.3m), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        var act = () => grade.MarkApplied(RequestedAt.AddSeconds(41));

        act.Should().Throw<InvalidOperationException>();
    }
}
