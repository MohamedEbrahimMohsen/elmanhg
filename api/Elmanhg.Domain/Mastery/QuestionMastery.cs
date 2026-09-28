using Core.DDD.Entities;

namespace Elmanhg.Domain.Mastery;

public class QuestionMastery : AuditEntity
{
    public Guid StudentId { get; private set; }
    public Guid QuestionId { get; private set; }
    public bool IsMastered { get; private set; }
    public Guid LatestAttemptId { get; private set; }
    public decimal LatestNormalisedScore { get; private set; }
    public DateTimeOffset LatestAttemptedAt { get; private set; }
    public Guid? PreviousAttemptId { get; private set; }
    public decimal? PreviousNormalisedScore { get; private set; }
    public DateTimeOffset? PreviousAttemptedAt { get; private set; }
    public uint Version { get; private set; }

    private QuestionMastery(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static QuestionMastery Start(Guid studentId, Guid questionId, MasteryAttempt attempt)
    {
        return new QuestionMastery(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            QuestionId = questionId,
            IsMastered = false,
            LatestAttemptId = attempt.AttemptId,
            LatestNormalisedScore = attempt.NormalisedScore,
            LatestAttemptedAt = attempt.AttemptedAt,
        };
    }

    // PRD §7.3: mastered = the two most recent attempts, by attempt time, both at or above the threshold.
    public void Record(MasteryAttempt attempt, decimal correctThreshold)
    {
        if (attempt.AttemptId == LatestAttemptId || attempt.AttemptId == PreviousAttemptId)
        {
            return;
        }

        if (attempt.AttemptedAt >= LatestAttemptedAt)
        {
            PreviousAttemptId = LatestAttemptId;
            PreviousNormalisedScore = LatestNormalisedScore;
            PreviousAttemptedAt = LatestAttemptedAt;
            LatestAttemptId = attempt.AttemptId;
            LatestNormalisedScore = attempt.NormalisedScore;
            LatestAttemptedAt = attempt.AttemptedAt;
        }
        else if (PreviousAttemptedAt is null || attempt.AttemptedAt > PreviousAttemptedAt)
        {
            PreviousAttemptId = attempt.AttemptId;
            PreviousNormalisedScore = attempt.NormalisedScore;
            PreviousAttemptedAt = attempt.AttemptedAt;
        }
        else
        {
            return;
        }

        IsMastered = PreviousNormalisedScore is not null && LatestNormalisedScore >= correctThreshold && PreviousNormalisedScore >= correctThreshold;
        UpdatedBy = StudentId;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
