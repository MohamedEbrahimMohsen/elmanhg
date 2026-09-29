using Elmanhg.Domain.TeacherThreads;
using System.Linq.Expressions;

namespace Elmanhg.Application.TeacherInbox.Shared;

public static class TeacherInboxQueryShape
{
    public static Expression<Func<TeacherThread, bool>> Filter(IReadOnlyCollection<Guid>? subjectIds, TeacherInboxFilter filter, Guid callerId)
    {
        return x => (subjectIds == null || subjectIds.Contains(x.SubjectId)) && (filter != TeacherInboxFilter.Unclaimed || x.TeacherId == null) && (filter != TeacherInboxFilter.Mine || x.TeacherId == callerId);
    }

    public static IOrderedQueryable<TeacherThread> Order(IQueryable<TeacherThread> query)
    {
        return query
            .OrderBy(x => x.Status == TeacherThreadStatus.Open ? 0 : 1)
            .ThenBy(x => x.SlaDueAt)
            .ThenBy(x => x.Id);
    }
}
