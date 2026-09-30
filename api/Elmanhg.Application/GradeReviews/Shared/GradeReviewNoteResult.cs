namespace Elmanhg.Application.GradeReviews.Shared;

public sealed record GradeReviewNoteResult(string Decision, string? Comment, DateTimeOffset ReviewedAt);
