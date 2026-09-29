using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherInbox.Shared;

public static class TeacherInboxNames
{
    public static async Task<Dictionary<Guid, string>> LoadAsync(IUserRepository userRepository, IReadOnlyCollection<TeacherThread> threads, CancellationToken cancellationToken)
    {
        var ids = threads
            .Select(x => x.StudentId)
            .Concat(threads.Where(x => x.TeacherId != null).Select(x => x.TeacherId!.Value))
            .Distinct()
            .ToList();
        var users = await userRepository.FindAsync(x => ids.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return users.ToDictionary(x => x.Id, x => x.DisplayName);
    }
}
