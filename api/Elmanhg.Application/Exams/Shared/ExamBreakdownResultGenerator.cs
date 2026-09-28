using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Exams.Shared;

public static class ExamBreakdownResultGenerator
{
    public static List<ExamLessonResult> Lessons(IEnumerable<ExamShare> shares, IReadOnlyCollection<Lesson> lessons)
    {
        return shares
            .Select(x => new ExamLessonResult(x.Id, lessons.FirstOrDefault(lesson => lesson.Id == x.LessonId)?.Name, x.QuestionCount, x.CorrectCount, x.Score, x.MaxScore, x.ScorePercent))
            .ToList();
    }

    public static List<ExamObjectiveResult> Objectives(IEnumerable<ExamShare> shares, IReadOnlyCollection<Lesson> lessons)
    {
        return shares
            .Select(x => Objective(x, lessons.FirstOrDefault(lesson => lesson.Id == x.LessonId)))
            .OfType<ExamObjectiveResult>()
            .ToList();
    }

    public static List<ExamUnitBreakdownResult> Units(IEnumerable<ExamUnitShare> shares, IReadOnlyCollection<CurriculumUnit> units)
    {
        return shares
            .Select(x => new ExamUnitBreakdownResult(x.UnitId, units.FirstOrDefault(unit => unit.Id == x.UnitId)?.Name, x.QuestionCount, x.CorrectCount, x.Score, x.MaxScore, x.ScorePercent))
            .ToList();
    }

    private static ExamObjectiveResult? Objective(ExamShare share, Lesson? lesson)
    {
        var objective = lesson?.Objectives.FirstOrDefault(x => x.Id == share.Id);
        return objective is null ? null : new ExamObjectiveResult(share.Id, objective.Text, share.LessonId, lesson?.Name, share.QuestionCount, share.ScorePercent);
    }
}
