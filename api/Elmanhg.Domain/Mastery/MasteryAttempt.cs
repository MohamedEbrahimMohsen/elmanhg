using Elmanhg.Domain.Sessions;

namespace Elmanhg.Domain.Mastery;

public sealed record MasteryAttempt(Guid AttemptId, decimal NormalisedScore, DateTimeOffset AttemptedAt)
{
    public static MasteryAttempt From(Attempt attempt) => new(attempt.Id, attempt.NormalisedScore, attempt.CreatedAt);
}
