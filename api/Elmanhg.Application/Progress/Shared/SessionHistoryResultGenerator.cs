using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Progress.Shared;

public static class SessionHistoryResultGenerator
{
    public static SessionHistoryItemResult Generate(Session session, IReadOnlyList<Lesson> lessons, IReadOnlyList<CurriculumUnit> units, IReadOnlyDictionary<string, decimal> bestByScopeKey)
    {
        var (lessonId, unitId, scopeName) = session.Kind switch
        {
            SessionKind.Quiz => ForLesson(QuizScope.FromJson(session.Scope).LessonId, lessons),
            SessionKind.UnitExam => ForUnit(session.GetExamUnitIds()[0], units),
            SessionKind.MultiUnitExam => ForUnits(session.GetExamUnitIds(), units),
            _ => (null, null, null),
        };
        return new SessionHistoryItemResult(session.Id, session.Kind.ToString(), lessonId, unitId, scopeName, session.StartedAt, session.SubmittedAt, session.ScorePercent, ExamBestScoreSpecification.IsSatisfiedBy(session) && bestByScopeKey.TryGetValue(session.ScopeKey, out var best) && best == session.ScorePercent);
    }

    private static (Guid? LessonId, Guid? UnitId, string? ScopeName) ForLesson(Guid lessonId, IReadOnlyList<Lesson> lessons) => (lessonId, null, lessons.FirstOrDefault(x => x.Id == lessonId)?.Name);

    private static (Guid? LessonId, Guid? UnitId, string? ScopeName) ForUnit(Guid unitId, IReadOnlyList<CurriculumUnit> units) => (null, unitId, units.FirstOrDefault(x => x.Id == unitId)?.Name);

    private static (Guid? LessonId, Guid? UnitId, string? ScopeName) ForUnits(IReadOnlyList<Guid> unitIds, IReadOnlyList<CurriculumUnit> units)
    {
        var names = unitIds
            .Select(id => units.FirstOrDefault(x => x.Id == id)?.Name)
            .OfType<string>()
            .ToList();
        return (null, null, names.Count == 0 ? null : string.Join(" + ", names));
    }
}
