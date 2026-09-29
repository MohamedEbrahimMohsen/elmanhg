namespace Elmanhg.Application.ContentRetrieval.Shared;

public sealed record LessonContentSearchResult(Guid LessonId, DateTimeOffset? IndexedAt, List<LessonContentMatchResult> Matches);
