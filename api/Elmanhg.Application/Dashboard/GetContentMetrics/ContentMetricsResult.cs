namespace Elmanhg.Application.Dashboard.GetContentMetrics;

public sealed record ContentMetricsResult(Guid? SubjectId, int Subjects, int Units, int LessonsDraft, int LessonsPublished, int LessonsArchived, int QuestionsPending, int QuestionsApproved, int QuestionsRejected, int QuestionsRetired, List<QuestionTypeCountResult> QuestionsByType, int ServableTotal, DateTimeOffset GeneratedAt);
