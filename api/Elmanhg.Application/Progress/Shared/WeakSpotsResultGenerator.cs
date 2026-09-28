using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;

namespace Elmanhg.Application.Progress.Shared;

public static class WeakSpotsResultGenerator
{
    public static WeakSpotsResult Generate(IReadOnlyList<LessonMasteryCount> weakLessons, IReadOnlyList<ObjectiveMasteryCount> weakObjectives, IReadOnlyList<Lesson> lessons, IReadOnlyList<Subject> subjects)
    {
        var lessonResults = weakLessons
            .Select(count => GenerateLesson(count, lessons, subjects))
            .OfType<WeakLessonResult>()
            .ToList();
        var objectiveResults = weakObjectives
            .Select(count => GenerateObjective(count, lessons, subjects))
            .OfType<WeakObjectiveResult>()
            .ToList();
        return new WeakSpotsResult(lessonResults, objectiveResults);
    }

    private static WeakLessonResult? GenerateLesson(LessonMasteryCount count, IReadOnlyList<Lesson> lessons, IReadOnlyList<Subject> subjects)
    {
        var lesson = lessons.FirstOrDefault(x => x.Id == count.LessonId);
        var subject = subjects.FirstOrDefault(x => x.Id == count.SubjectId);
        if (lesson is null || subject is null)
        {
            return null;
        }

        var totals = new MasteryTotals(count.ServableCount, count.MasteredCount, count.SeenCount);
        return new WeakLessonResult(lesson.Id, lesson.Name, subject.Id, subject.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent);
    }

    private static WeakObjectiveResult? GenerateObjective(ObjectiveMasteryCount count, IReadOnlyList<Lesson> lessons, IReadOnlyList<Subject> subjects)
    {
        var lesson = lessons.FirstOrDefault(x => x.Id == count.LessonId);
        var objective = lesson?.Objectives.FirstOrDefault(x => x.Id == count.ObjectiveId);
        var subject = subjects.FirstOrDefault(x => x.Id == count.SubjectId);
        if (lesson is null || objective is null || subject is null)
        {
            return null;
        }

        var totals = new MasteryTotals(count.ServableCount, count.MasteredCount, count.SeenCount);
        return new WeakObjectiveResult(objective.Id, objective.Text, lesson.Id, lesson.Name, subject.Id, subject.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent);
    }
}
