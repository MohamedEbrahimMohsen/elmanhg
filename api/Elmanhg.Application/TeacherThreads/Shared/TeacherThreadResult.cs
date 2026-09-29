using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherThreads.Shared;

public sealed record TeacherThreadResult(Guid Id, TeacherThreadContextResult Context, TeacherThreadStatus Status, bool IsOverdue, DateTimeOffset SubmittedAt, DateTimeOffset SlaDueAt, List<TeacherMessageResult> Messages, bool HasUnreadReply);
