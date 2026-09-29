using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherThreads.Shared;

public sealed record TeacherThreadSummaryResult(Guid Id, string SubjectName, string LessonName, string QuestionText, TeacherThreadStatus Status, bool IsOverdue, DateTimeOffset SubmittedAt, DateTimeOffset SlaDueAt, bool HasUnreadReply);
