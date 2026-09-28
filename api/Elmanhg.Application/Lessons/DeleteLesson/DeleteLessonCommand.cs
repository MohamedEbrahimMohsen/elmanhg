using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Lessons.DeleteLesson;

public sealed record DeleteLessonCommand(Guid LessonId) : IRequest, IAuditableCommand
{
    public string AuditAction => "Lesson.Delete";
    public string AuditResourceType => "Lesson";
    public Guid? AuditResourceId => LessonId;
}
