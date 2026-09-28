namespace Elmanhg.Application.Progress.Shared;

public sealed record SessionHistoryItemResult(Guid Id, string Kind, Guid? LessonId, Guid? UnitId, string? ScopeName, DateTimeOffset StartedAt, DateTimeOffset? SubmittedAt, decimal? ScorePercent, bool IsBestScore);
