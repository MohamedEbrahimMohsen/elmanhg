using Core.DDD.Time;
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
        var at = gradedAt.TruncateToMicroseconds();
        var needsReview = assessment.Confidence < reviewConfidenceThreshold;
        Retry.RecordSuccess();
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
        RaiseDomainEvent(new EssayGradeCompleted(this));
        UpdationDate = at;
    }

    public void FailAttempt(string errorCode, DateTimeOffset failedAt, int maxAttempts, TimeSpan retryBaseDelay)
    {
        EnsurePending();
        var at = failedAt.TruncateToMicroseconds();
        if (Retry.RecordFailure(errorCode, at, maxAttempts, retryBaseDelay))
        {
            Status = EssayGradeStatus.InReview;
            ReviewReason = EssayReviewReason.GradingFailed;
        }

        UpdationDate = at;
    }

    public QuestionGrade ToQuestionGrade()
    {
        if (Status != EssayGradeStatus.Graded || FinalScore is null || FinalNormalisedScore is null)
        {
            throw new InvalidOperationException("Only a graded essay has a final score.");
        }

        return new QuestionGrade(FinalScore.Value, FinalNormalisedScore.Value, QuestionGrade.ToOutcome(FinalNormalisedScore.Value), null);
    }

    public void MarkApplied(DateTimeOffset appliedAt)
    {
        if (!IsAwaitingApplication)
        {
            throw new InvalidOperationException("Only a graded essay that is not yet applied can be applied.");
        }

        var at = appliedAt.TruncateToMicroseconds();
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
