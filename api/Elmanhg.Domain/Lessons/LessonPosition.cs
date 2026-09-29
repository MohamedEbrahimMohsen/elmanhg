namespace Elmanhg.Domain.Lessons;

public sealed record LessonPosition(Guid Id, Guid UnitId, int Order, DateTimeOffset CreationDate)
{
    public static LessonPosition Of(Lesson lesson) => new(lesson.Id, lesson.UnitId, lesson.Order, lesson.CreationDate);
}
