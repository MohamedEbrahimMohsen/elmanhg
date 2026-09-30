using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Dashboard.GetSuccessRateMetrics;

public static class SuccessRateMetricsResultGenerator
{
    public static SuccessRateMetricsResult Generate(DashboardWindow window, Guid? subjectId, List<LessonAttemptOutcome> outcomes, List<Subject> subjects, List<CurriculumUnit> units, List<Lesson> lessons)
    {
        var subjectById = subjects.ToDictionary(x => x.Id);
        var unitById = units.ToDictionary(x => x.Id);
        var lessonById = lessons.ToDictionary(x => x.Id);
        var bySubject = outcomes
            .GroupBy(x => x.SubjectId)
            .OrderBy(x => subjectById.GetValueOrDefault(x.Key)?.Order ?? int.MaxValue)
            .ThenBy(x => subjectById.GetValueOrDefault(x.Key)?.Name, StringComparer.Ordinal)
            .Select(x => Group(x.Key, subjectById.GetValueOrDefault(x.Key)?.Name, null, x))
            .ToList();
        var byUnit = outcomes
            .GroupBy(x => (x.SubjectId, x.UnitId))
            .OrderBy(x => subjectById.GetValueOrDefault(x.Key.SubjectId)?.Order ?? int.MaxValue)
            .ThenBy(x => unitById.GetValueOrDefault(x.Key.UnitId)?.Order ?? int.MaxValue)
            .ThenBy(x => unitById.GetValueOrDefault(x.Key.UnitId)?.Name, StringComparer.Ordinal)
            .Select(x => Group(x.Key.UnitId, unitById.GetValueOrDefault(x.Key.UnitId)?.Name, x.Key.SubjectId, x))
            .ToList();
        var byLesson = outcomes
            .OrderBy(x => subjectById.GetValueOrDefault(x.SubjectId)?.Order ?? int.MaxValue)
            .ThenBy(x => unitById.GetValueOrDefault(x.UnitId)?.Order ?? int.MaxValue)
            .ThenBy(x => lessonById.GetValueOrDefault(x.LessonId)?.Order ?? int.MaxValue)
            .ThenBy(x => lessonById.GetValueOrDefault(x.LessonId)?.Name, StringComparer.Ordinal)
            .Select(x => Group(x.LessonId, lessonById.GetValueOrDefault(x.LessonId)?.Name, x.UnitId, [x]))
            .ToList();
        var attempts = outcomes.Sum(x => x.Attempts);
        var correct = outcomes.Sum(x => x.Correct);

        return new SuccessRateMetricsResult(window.From, window.To, subjectId, attempts, correct, DashboardRates.Ratio(correct, attempts, DashboardRates.RateDecimals), bySubject, byUnit, byLesson, window.Now);
    }

    private static SuccessRateGroupResult Group(Guid id, string? name, Guid? parentId, IEnumerable<LessonAttemptOutcome> outcomes)
    {
        var attempts = outcomes.Sum(x => x.Attempts);
        var correct = outcomes.Sum(x => x.Correct);
        return new SuccessRateGroupResult(id, name ?? string.Empty, parentId, attempts, correct, DashboardRates.Ratio(correct, attempts, DashboardRates.RateDecimals));
    }
}
