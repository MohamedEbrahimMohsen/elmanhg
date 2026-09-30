using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.Shared.Realtime;

public interface ITeacherThreadNotifier
{
    Task NotifyReplyAsync(Guid studentId, Guid threadId, CancellationToken cancellationToken);
    Task NotifyReminderAsync(IReadOnlyCollection<Guid> teacherIds, Guid threadId, TeacherThreadSlaEventKind kind, CancellationToken cancellationToken);
}
