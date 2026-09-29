using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherInbox.Shared;

public sealed record TeacherInboxReminderResult(Guid ThreadId, string SubjectName, string LessonName, string QuestionText, TeacherThreadSlaEventKind Kind, bool IsClaimedByMe, bool IsOverdue, DateTimeOffset SlaDueAt);
