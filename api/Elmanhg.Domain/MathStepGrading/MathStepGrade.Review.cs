using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.MathStepGrading;

public partial class MathStepGrade : IAuditedEntity
{
    public GradeReviewDecision? ReviewDecision { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewComment { get; private set; }
    public decimal? ReviewedScore { get; private set; }
    public decimal? ReviewedNormalisedScore { get; private set; }

    public decimal? FinalScore => ReviewedScore ?? Score;

    public decimal? FinalNormalisedScore => ReviewedNormalisedScore ?? NormalisedScore;

    public AttemptGrader GradedBy => ReviewDecision is null ? AttemptGrader.AI : AttemptGrader.Teacher;

    public void Accept(Guid teacherId, string? comment, DateTimeOffset reviewedAt)
    {
        EnsureInReview();
        if (Score is null || NormalisedScore is null)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.GradeReviewNoAiScore);
        }

        Resolve(GradeReviewDecision.Accepted, Score.Value, NormalisedScore.Value, teacherId, comment, reviewedAt);
    }

    public void Override(decimal score, Guid teacherId, string? comment, DateTimeOffset reviewedAt)
    {
        EnsureInReview();
        if (score < 0 || score > MaxScore)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.GradeReviewScoreOutOfRange);
        }

        Resolve(GradeReviewDecision.Overridden, score, GradeReviewScore.Normalise(score, MaxScore), teacherId, comment, reviewedAt);
    }

    private void Resolve(GradeReviewDecision decision, decimal score, decimal normalisedScore, Guid teacherId, string? comment, DateTimeOffset reviewedAt)
    {
        var at = ToMicroseconds(reviewedAt);
        ReviewDecision = decision;
        ReviewedScore = score;
        ReviewedNormalisedScore = normalisedScore;
        ReviewComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        ReviewedBy = teacherId;
        ReviewedAt = at;
        GradedAt ??= at;
        Status = MathStepGradeStatus.Graded;
        UpdatedBy = teacherId;
        UpdationDate = at;
    }

    private void EnsureInReview()
    {
        if (Status != MathStepGradeStatus.InReview)
        {
            throw new ConflictCoreException(ErrorCodes.GradeNotInReview);
        }
    }
}
