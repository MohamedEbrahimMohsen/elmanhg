using Core.Errors;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.SharedKernel.Exceptions;
using System.Text.Json;

namespace Elmanhg.Domain.MathStepGrading;

public partial class MathStepGrade
{
    public void RecordVerdict(MathAnswerVerdict verdict, DateTimeOffset checkedAt)
    {
        EnsurePending();
        if (verdict == MathAnswerVerdict.Unchecked || FinalAnswerVerdict is not null)
        {
            throw new InvalidOperationException("Only a checked verdict is recorded, and only once.");
        }

        FinalAnswerVerdict = verdict;
        UpdationDate = ToMicroseconds(checkedAt);
    }

    public void Complete(MathStepAssessment? assessment, QuestionGrade grade, decimal reviewConfidenceThreshold, DateTimeOffset gradedAt)
    {
        EnsurePending();
        if (FinalAnswerVerdict is null)
        {
            throw new InvalidOperationException("Math steps are graded after the final answer is checked.");
        }

        var at = ToMicroseconds(gradedAt);
        var needsReview = assessment is not null && assessment.Confidence < reviewConfidenceThreshold;
        Attempts++;
        NextAttemptAt = null;
        LastErrorCode = null;
        Score = grade.Score;
        NormalisedScore = grade.NormalisedScore;
        Feedback = grade.Feedback is null ? null : JsonSerializer.Serialize(grade.Feedback, QuestionJson.SerializerOptions);
        Steps = assessment is null ? null : JsonSerializer.Serialize(assessment.Steps, QuestionJson.SerializerOptions);
        Justification = assessment?.Justification;
        Confidence = assessment?.Confidence;
        Model = assessment?.Model;
        PromptVersion = assessment?.PromptVersion;
        InputTokens = assessment?.InputTokens;
        OutputTokens = assessment?.OutputTokens;
        CostUsd = assessment?.CostUsd;
        GradedAt = at;
        Status = needsReview ? MathStepGradeStatus.InReview : MathStepGradeStatus.Graded;
        ReviewReason = needsReview ? MathStepReviewReason.LowConfidence : null;
        UpdationDate = at;
    }

    public void FailAttempt(string errorCode, DateTimeOffset failedAt, int maxAttempts, TimeSpan retryBaseDelay)
    {
        EnsurePending();
        var at = ToMicroseconds(failedAt);
        Attempts++;
        LastErrorCode = errorCode.Length <= ErrorCodeMaxLength ? errorCode : errorCode[..ErrorCodeMaxLength];
        if (Attempts >= maxAttempts)
        {
            Status = MathStepGradeStatus.InReview;
            ReviewReason = FinalAnswerVerdict is null ? MathStepReviewReason.FinalAnswerUnchecked : MathStepReviewReason.GradingFailed;
            NextAttemptAt = null;
        }
        else
        {
            NextAttemptAt = at + (retryBaseDelay * Math.Pow(2, Attempts - 1));
        }

        UpdationDate = at;
    }

    public QuestionGrade ToQuestionGrade()
    {
        if (Status != MathStepGradeStatus.Graded || FinalScore is null || FinalNormalisedScore is null)
        {
            throw new InvalidOperationException("Only a graded math answer has a final score.");
        }

        var feedback = ReviewDecision == GradeReviewDecision.Overridden || Feedback is null ? null : JsonSerializer.Deserialize<GradeFeedback>(Feedback, QuestionJson.SerializerOptions);
        return new QuestionGrade(FinalScore.Value, FinalNormalisedScore.Value, QuestionGrade.ToOutcome(FinalNormalisedScore.Value), feedback);
    }

    public void MarkApplied(DateTimeOffset appliedAt)
    {
        if (!IsAwaitingApplication)
        {
            throw new InvalidOperationException("Only a graded math answer that is not yet applied can be applied.");
        }

        var at = ToMicroseconds(appliedAt);
        AppliedAt = at;
        UpdationDate = at;
    }

    private void EnsurePending()
    {
        if (Status != MathStepGradeStatus.Pending)
        {
            throw new ConflictCoreException(ErrorCodes.MathStepGradeNotPending);
        }
    }
}
