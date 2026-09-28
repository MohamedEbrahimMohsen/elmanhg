namespace Elmanhg.Domain.Sessions;

public sealed record ExamAttemptSummary(Guid SessionId, DateTimeOffset SubmittedAt, decimal ScorePercent);
