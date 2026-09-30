using Core.DDD.Entities;

namespace Elmanhg.Domain.Questions;

public class QuestionDecision : Entity
{
    public Guid QuestionId { get; private set; }
    public int Version { get; private set; }
    public QuestionDecisionOutcome Outcome { get; private set; }
    public string? Reason { get; private set; }
    public QuestionDifficulty Difficulty { get; private set; }
    public QuestionDifficulty? DifficultyChangedFrom { get; private set; }
    public Guid DecidedBy { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }

    private QuestionDecision(Guid id) : base(id) { }

    internal static QuestionDecision Create(Guid questionId, int version, QuestionDecisionOutcome outcome, string? reason, QuestionDifficulty difficulty, QuestionDifficulty? difficultyChangedFrom, Guid decidedBy, DateTimeOffset submittedAt, DateTimeOffset decidedAt)
    {
        return new QuestionDecision(Guid.NewGuid())
        {
            QuestionId = questionId,
            Version = version,
            Outcome = outcome,
            Reason = reason,
            Difficulty = difficulty,
            DifficultyChangedFrom = difficultyChangedFrom,
            DecidedBy = decidedBy,
            SubmittedAt = submittedAt,
            DecidedAt = decidedAt,
        };
    }
}
