using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Lessons.PublishLesson;

public sealed record PublishLessonCommand(Guid LessonId) : IRequest, IAuditableCommand
{
    public string AuditAction => "Lesson.Publish";
    public string AuditResourceType => "Lesson";
    public Guid? AuditResourceId => LessonId;
}
