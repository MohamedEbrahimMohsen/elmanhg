using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Lessons;

public class LessonOpening : Entity
{
    public Guid StudentId { get; private set; }
    public Guid LessonId { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }

    private LessonOpening(Guid id) : base(id) { }

    public static LessonOpening Record(Guid studentId, Lesson lesson, DateTimeOffset openedAt)
    {
        if (lesson.State != LessonState.Published)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.LessonNotPublished);
        }

        return new LessonOpening(Guid.NewGuid())
        {
            StudentId = studentId,
            LessonId = lesson.Id,
            OpenedAt = openedAt,
        };
    }
}
