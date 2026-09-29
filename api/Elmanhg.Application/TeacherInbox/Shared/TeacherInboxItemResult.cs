using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherInbox.Shared;

public sealed record TeacherInboxItemResult(Guid Id, string SubjectName, string LessonName, string QuestionText, string StudentName, string? TeacherName, bool IsClaimedByMe, TeacherThreadStatus Status, bool IsOverdue, DateTimeOffset SubmittedAt, DateTimeOffset SlaDueAt);
