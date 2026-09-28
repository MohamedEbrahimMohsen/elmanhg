using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Lessons.ReorderLesson;

public sealed record ReorderLessonCommand(Guid LessonId, int Position) : IRequest, IAuditableCommand
{
    public string AuditAction => "Lesson.Reorder";
    public string AuditResourceType => "Lesson";
    public Guid? AuditResourceId => LessonId;
}
