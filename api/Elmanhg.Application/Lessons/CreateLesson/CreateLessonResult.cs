using Core.Auditing;

namespace Elmanhg.Application.Lessons.CreateLesson;

public sealed record CreateLessonResult(Guid Id) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => Id;
}
