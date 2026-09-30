using Core.Errors;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.SharedKernel.Exceptions;
using System.Text.Json;

namespace Elmanhg.Domain.EssayGrading;

public partial class EssayGrade
{
    public void Complete(EssayAssessment assessment, QuestionGrade grade, decimal reviewConfidenceThreshold, DateTimeOffset gradedAt)
    {
        EnsurePending();
        var at = ToMicroseconds(gradedAt);
        var needsReview = assessment.Confidence < reviewConfidenceThreshold;
        Attempts++;
        NextAttemptAt = null;
        LastErrorCode = null;
        Score = grade.Score;
        NormalisedScore = grade.NormalisedScore;
        Criteria = JsonSerializer.Serialize(assessment.Criteria, QuestionJson.SerializerOptions);
        Justification = assessment.Justification;
        Confidence = assessment.Confidence;
        Model = assessment.Model;
        PromptVersion = assessment.PromptVersion;
        InputTokens = assessment.InputTokens;
        OutputTokens = assessment.OutputTokens;
        CostUsd = assessment.CostUsd;
        GradedAt = at;
        Status = needsReview ? EssayGradeStatus.InReview : EssayGradeStatus.Graded;
        ReviewReason = needsReview ? EssayReviewReason.LowConfidence : null;
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
            Status = EssayGradeStatus.InReview;
            ReviewReason = EssayReviewReason.GradingFailed;
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
        if (Status != EssayGradeStatus.Graded || Score is null || NormalisedScore is null)
        {
            throw new InvalidOperationException("Only a graded essay has a final score.");
        }

        return new QuestionGrade(Score.Value, NormalisedScore.Value, QuestionGrade.ToOutcome(NormalisedScore.Value), null);
    }

    public void MarkApplied(DateTimeOffset appliedAt)
    {
        if (!IsAwaitingApplication)
        {
            throw new InvalidOperationException("Only a graded essay that is not yet applied can be applied.");
        }

        var at = ToMicroseconds(appliedAt);
        AppliedAt = at;
        UpdationDate = at;
    }

    private void EnsurePending()
    {
        if (Status != EssayGradeStatus.Pending)
        {
            throw new ConflictCoreException(ErrorCodes.EssayGradeNotPending);
        }
    }
}
