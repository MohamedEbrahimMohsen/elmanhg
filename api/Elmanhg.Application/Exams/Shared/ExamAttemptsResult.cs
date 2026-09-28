namespace Elmanhg.Application.Exams.Shared;

public sealed record ExamAttemptsResult(decimal? BestScorePercent, List<ExamAttemptResult> Attempts);

public sealed record ExamAttemptResult(Guid SessionId, DateTimeOffset SubmittedAt, decimal ScorePercent, bool IsBest);
