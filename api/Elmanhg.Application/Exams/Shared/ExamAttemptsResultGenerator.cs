using Elmanhg.Domain.Sessions;

namespace Elmanhg.Application.Exams.Shared;

public static class ExamAttemptsResultGenerator
{
    public static ExamAttemptsResult Generate(IReadOnlyList<ExamAttemptSummary> attempts)
    {
        decimal? best = attempts.Count == 0 ? null : attempts.Max(x => x.ScorePercent);
        return new ExamAttemptsResult(best, attempts
            .Select(x => new ExamAttemptResult(x.SessionId, x.SubmittedAt, x.ScorePercent, x.ScorePercent == best))
            .ToList());
    }
}
