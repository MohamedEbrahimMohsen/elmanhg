using Core.DDD.Repositories;

namespace Elmanhg.Domain.TeacherThreads;

public interface ITeacherThreadOutOfAppReminderRepository : IRepository<TeacherThreadOutOfAppReminder>
{
    Task<bool> IsRecordedAsync(Guid threadId, CancellationToken cancellationToken);
}
