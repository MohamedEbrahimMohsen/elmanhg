using Elmanhg.Domain.Lessons;

namespace Elmanhg.Application.Lessons.Shared;

public static class LessonResultGenerator
{
    public static LessonResult Generate(Lesson lesson)
    {
        return new LessonResult(lesson.Id, lesson.UnitId, lesson.Name, lesson.Order, lesson.State.ToString());
    }

    public static LessonDetailResult GenerateDetail(Lesson lesson)
    {
        var objectives = lesson.Objectives
            .OrderBy(x => x.Order)
            .Select(x => new LessonObjectiveResult(x.Id, x.Text, x.Order))
            .ToList();

        return new LessonDetailResult(lesson.Id, lesson.UnitId, lesson.Name, lesson.Order, lesson.State.ToString(), lesson.Explanation, lesson.Summary, lesson.VideoUrl, objectives);
    }
}
