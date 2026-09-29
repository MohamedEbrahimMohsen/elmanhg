using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherInbox.Shared;

public sealed record TeacherInboxThreadResult(Guid Id, TeacherThreadContextResult Context, string StudentName, string? TeacherName, bool IsClaimedByMe, bool CanClaim, bool CanReply, TeacherThreadStatus Status, bool IsOverdue, DateTimeOffset SubmittedAt, DateTimeOffset SlaDueAt, DateTimeOffset? ClaimedAt, List<TeacherMessageResult> Messages);
