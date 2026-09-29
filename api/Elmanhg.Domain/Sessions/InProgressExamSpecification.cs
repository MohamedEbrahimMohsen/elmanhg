using System.Linq.Expressions;

namespace Elmanhg.Domain.Sessions;

// PRD §17 rule 10: an exam in progress is open and still accepts saves; the Avatar refuses every message while one exists.
public static class InProgressExamSpecification
{
    public static Expression<Func<Session, bool>> For(Guid studentId, DateTimeOffset now, TimeSpan grace)
    {
        var expiredBefore = now - grace;
        return x => x.StudentId == studentId && x.Kind != SessionKind.Quiz && x.SubmittedAt == null && (x.Deadline == null || x.Deadline >= expiredBefore);
    }
}
