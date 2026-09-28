namespace Elmanhg.Application.Questions.Shared;

public sealed record QuestionListItemResult(Guid Id, Guid LessonId, string LessonName, Guid SubjectId, string Type, string Stem, string Difficulty, int Version, string ValidationStatus, Guid? ValidatedBy, string? TeacherName, string? RejectionReason, DateTimeOffset UpdatedAt);
