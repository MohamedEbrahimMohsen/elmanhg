using Core.Errors;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.MathStepGrading;

public sealed class MathStepGradeTests
{
    private static readonly DateTimeOffset RequestedAt = MathStepGradeBuilder.DefaultRequestedAt;
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(30);
    private static readonly QuestionGrade PartialGrade = new(1.5m, 0.75m, GradeOutcome.Partial, GradeFeedback.MathStepTally(1, 2));

    [Fact]
    public void Request_NewGrade_IsPendingAndDueAtRequest()
    {
        var studentId = Guid.NewGuid();
        var grade = new MathStepGradeBuilder().ForStudent(studentId).WithTimeTaken(42_000).RequestedAt(RequestedAt.AddTicks(7)).Build();

        (grade.Status, grade.Attempts, grade.RequestedAt, grade.NextAttemptAt).Should().Be((MathStepGradeStatus.Pending, 0, RequestedAt, (DateTimeOffset?)RequestedAt));
        (grade.StudentId, grade.CreatedBy, grade.FinalAnswerVerdict, grade.TimeTakenMilliseconds, grade.MaxScore).Should().Be((studentId, (Guid?)studentId, (MathAnswerVerdict?)MathAnswerVerdict.Equivalent, 42_000, 2));
        grade.IsDueAt(grade.RequestedAt).Should().BeTrue();
        grade.ReadAnswer().FinalAnswer.Should().Be("x = 2");
    }

    [Fact]
    public void Request_UncheckedVerdict_StoresNoVerdict()
    {
        var grade = new MathStepGradeBuilder().WithVerdict(MathAnswerVerdict.Unchecked).Build();

        grade.FinalAnswerVerdict.Should().BeNull();
    }

    [Fact]
    public void Request_BlankFinalAnswer_ThrowsInvalidOperationException()
    {
        var act = () => new MathStepGradeBuilder().WithAnswer("""{"steps":["2x = 4"],"finalAnswer":"  "}""").Build();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Request_NegativeTimeTaken_ThrowsArgumentOutOfRange()
    {
        var act = () => new MathStepGradeBuilder().WithTimeTaken(-1).Build();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RecordVerdict_Pending_StoresVerdict()
    {
        var grade = new MathStepGradeBuilder().WithVerdict(null).Build();

        grade.RecordVerdict(MathAnswerVerdict.NotEquivalent, RequestedAt.AddSeconds(30).AddTicks(3));

        (grade.FinalAnswerVerdict, grade.UpdationDate, grade.Status).Should().Be(((MathAnswerVerdict?)MathAnswerVerdict.NotEquivalent, RequestedAt.AddSeconds(30), MathStepGradeStatus.Pending));
    }

    [Fact]
    public void RecordVerdict_Unchecked_ThrowsInvalidOperation()
    {
        var grade = new MathStepGradeBuilder().WithVerdict(null).Build();

        var act = () => grade.RecordVerdict(MathAnswerVerdict.Unchecked, RequestedAt);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RecordVerdict_AlreadyChecked_ThrowsInvalidOperation()
    {
        var grade = new MathStepGradeBuilder().Build();

        var act = () => grade.RecordVerdict(MathAnswerVerdict.NotEquivalent, RequestedAt);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_ConfidentAssessment_StoresResultAndMarksGraded()
    {
        var grade = new MathStepGradeBuilder().Build();
        var assessment = MathStepGradeBuilder.Assessment(0.9m);
        var gradedAt = RequestedAt.AddSeconds(40);

        grade.Complete(assessment, PartialGrade, 0.7m, gradedAt.AddTicks(3));

        (grade.Status, grade.ReviewReason, grade.Score, grade.NormalisedScore).Should().Be((MathStepGradeStatus.Graded, (MathStepReviewReason?)null, (decimal?)1.5m, (decimal?)0.75m));
        grade.ReadSteps().Should().Equal(assessment.Steps);
        (grade.Justification, grade.Confidence, grade.Model, grade.PromptVersion).Should().Be(("جيد", (decimal?)0.9m, "claude-sonnet-5", "v1"));
        (grade.InputTokens, grade.OutputTokens, grade.CostUsd).Should().Be(((int?)900, (int?)150, (decimal?)0.00495m));
        (grade.Attempts, grade.NextAttemptAt, grade.GradedAt, grade.UpdationDate).Should().Be((1, (DateTimeOffset?)null, (DateTimeOffset?)gradedAt, gradedAt));
    }

    [Fact]
    public void Complete_LowConfidence_MarksInReviewForLowConfidence()
    {
        var grade = new MathStepGradeBuilder().Build();

        grade.Complete(MathStepGradeBuilder.Assessment(0.5m), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        (grade.Status, grade.ReviewReason, grade.Score).Should().Be((MathStepGradeStatus.InReview, (MathStepReviewReason?)MathStepReviewReason.LowConfidence, (decimal?)1.5m));
    }

    [Fact]
    public void Complete_ConfidenceAtThreshold_MarksGraded()
    {
        var grade = new MathStepGradeBuilder().Build();

        grade.Complete(MathStepGradeBuilder.Assessment(0.7m), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        grade.Status.Should().Be(MathStepGradeStatus.Graded);
    }

    [Fact]
    public void Complete_WithoutAssessment_MarksGradedWithoutAiFields()
    {
        var grade = new MathStepGradeBuilder().Build();
        var finalOnly = new QuestionGrade(2m, 1m, GradeOutcome.Correct, GradeFeedback.MathFinalAnswerOnly);

        grade.Complete(null, finalOnly, 0.7m, RequestedAt.AddSeconds(40));

        (grade.Status, grade.Score, grade.Model, grade.Confidence, grade.Steps, grade.Justification).Should().Be((MathStepGradeStatus.Graded, (decimal?)2m, (string?)null, (decimal?)null, (string?)null, (string?)null));
        grade.ReadSteps().Should().BeEmpty();
    }

    [Fact]
    public void Complete_WithoutVerdict_ThrowsInvalidOperation()
    {
        var grade = new MathStepGradeBuilder().WithVerdict(null).Build();

        var act = () => grade.Complete(MathStepGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_NotPending_ThrowsConflictMathStepGradeNotPending()
    {
        var grade = new MathStepGradeBuilder().Build();
        grade.Complete(MathStepGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        var act = () => grade.Complete(MathStepGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(50));

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.MathStepGradeNotPending);
    }

    [Fact]
    public void FailAttempt_BelowMax_SchedulesExponentialRetry()
    {
        var grade = new MathStepGradeBuilder().Build();
        var firstFailure = RequestedAt.AddSeconds(5);
        var secondFailure = RequestedAt.AddSeconds(50);

        grade.FailAttempt("MATH_STEP_GRADING_UNAVAILABLE", firstFailure, 4, RetryBaseDelay);
        var firstRetry = grade.NextAttemptAt;
        grade.FailAttempt("MATH_STEP_GRADING_UNAVAILABLE", secondFailure, 4, RetryBaseDelay);

        firstRetry.Should().Be(firstFailure.AddSeconds(30));
        (grade.NextAttemptAt, grade.Attempts, grade.LastErrorCode, grade.Status).Should().Be(((DateTimeOffset?)secondFailure.AddSeconds(60), 2, "MATH_STEP_GRADING_UNAVAILABLE", MathStepGradeStatus.Pending));
    }

    [Fact]
    public void FailAttempt_ReachesMaxWithVerdict_MarksInReviewForGradingFailed()
    {
        var grade = new MathStepGradeBuilder().Build();

        grade.FailAttempt("MATH_STEP_GRADING_UNAVAILABLE", RequestedAt.AddSeconds(5), 1, RetryBaseDelay);

        (grade.Status, grade.ReviewReason, grade.NextAttemptAt).Should().Be((MathStepGradeStatus.InReview, (MathStepReviewReason?)MathStepReviewReason.GradingFailed, (DateTimeOffset?)null));
    }

    [Fact]
    public void FailAttempt_ReachesMaxWithoutVerdict_MarksInReviewForFinalAnswerUnchecked()
    {
        var grade = new MathStepGradeBuilder().WithVerdict(null).Build();

        grade.FailAttempt("MATH_CHECK_UNAVAILABLE", RequestedAt.AddSeconds(5), 1, RetryBaseDelay);

        (grade.Status, grade.ReviewReason, grade.NextAttemptAt).Should().Be((MathStepGradeStatus.InReview, (MathStepReviewReason?)MathStepReviewReason.FinalAnswerUnchecked, (DateTimeOffset?)null));
    }

    [Fact]
    public void FailAttempt_LongErrorCode_TruncatesTo100()
    {
        var grade = new MathStepGradeBuilder().Build();

        grade.FailAttempt(new string('X', 150), RequestedAt.AddSeconds(5), 4, RetryBaseDelay);

        grade.LastErrorCode.Should().HaveLength(100);
    }

    [Fact]
    public void FailAttempt_NotPending_ThrowsConflictMathStepGradeNotPending()
    {
        var grade = new MathStepGradeBuilder().Build();
        grade.Complete(MathStepGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        var act = () => grade.FailAttempt("MATH_STEP_GRADING_UNAVAILABLE", RequestedAt.AddSeconds(50), 4, RetryBaseDelay);

        act.Should().Throw<ConflictCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.MathStepGradeNotPending);
    }

    [Fact]
    public void ToQuestionGrade_Graded_ReturnsScoreOutcomeAndStoredFeedback()
    {
        var grade = new MathStepGradeBuilder().Build();
        grade.Complete(MathStepGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        grade.ToQuestionGrade().Should().Be(new QuestionGrade(1.5m, 0.75m, GradeOutcome.Partial, GradeFeedback.MathStepTally(1, 2)));
    }

    [Fact]
    public void ToQuestionGrade_Pending_ThrowsInvalidOperation()
    {
        var grade = new MathStepGradeBuilder().Build();

        var act = () => grade.ToQuestionGrade();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkApplied_Graded_StampsAppliedAtAndStopsAwaiting()
    {
        var grade = new MathStepGradeBuilder().Build();
        grade.Complete(MathStepGradeBuilder.Assessment(), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));
        var awaitingBefore = grade.IsAwaitingApplication;

        grade.MarkApplied(RequestedAt.AddSeconds(41).AddTicks(3));

        (awaitingBefore, grade.IsAwaitingApplication, grade.AppliedAt).Should().Be((true, false, (DateTimeOffset?)RequestedAt.AddSeconds(41)));
    }

    [Fact]
    public void MarkApplied_InReview_ThrowsInvalidOperation()
    {
        var grade = new MathStepGradeBuilder().Build();
        grade.Complete(MathStepGradeBuilder.Assessment(0.3m), PartialGrade, 0.7m, RequestedAt.AddSeconds(40));

        var act = () => grade.MarkApplied(RequestedAt.AddSeconds(41));

        act.Should().Throw<InvalidOperationException>();
    }
}
