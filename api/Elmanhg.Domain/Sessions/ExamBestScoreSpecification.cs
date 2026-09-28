using System.Linq.Expressions;

namespace Elmanhg.Domain.Sessions;

// PRD §17 rule 9: the single definition of an exam sitting that counts toward a best score and the attempts list.
public static class ExamBestScoreSpecification
{
    public static readonly Expression<Func<Session, bool>> Condition = x => x.Kind != SessionKind.Quiz && !x.IsTestMode && x.SubmittedAt != null && x.ScorePercent != null;

    private static readonly Func<Session, bool> IsCounted = Condition.Compile();

    public static bool IsSatisfiedBy(Session session) => IsCounted(session);

    public static IQueryable<Session> WhereCountsTowardBestScore(this IQueryable<Session> sessions, Guid studentId)
    {
        return sessions
            .Where(x => x.StudentId == studentId)
            .Where(Condition);
    }
}
