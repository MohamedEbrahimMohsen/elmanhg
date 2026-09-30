namespace Elmanhg.Application.GradeReviews.Shared;

public sealed record GradeReviewItemResult(Guid Id, string Kind, Guid QuestionId, string Stem, string UnitName, string LessonName, string ReviewReason, int MaxScore, decimal? AiScore, decimal? Confidence, DateTimeOffset RequestedAt);
