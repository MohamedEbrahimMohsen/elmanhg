using Elmanhg.Domain.Teachers;
using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;

public static class TeacherThreadReminderRecipients
{
    public static async Task<List<Guid>> LoadAsync(TeacherThread thread, ITeacherSubjectRepository teacherSubjectRepository, CancellationToken cancellationToken)
    {
        if (thread.TeacherId is { } teacherId)
        {
            return [teacherId];
        }

        var assignments = await teacherSubjectRepository.FindAsync(x => x.SubjectId == thread.SubjectId, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return assignments
            .Select(x => x.TeacherId)
            .Distinct()
            .ToList();
    }
}
