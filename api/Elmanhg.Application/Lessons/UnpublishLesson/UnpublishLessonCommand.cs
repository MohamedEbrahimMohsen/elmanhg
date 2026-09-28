using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Lessons.UnpublishLesson;

public sealed record UnpublishLessonCommand(Guid LessonId) : IRequest, IAuditableCommand
{
    public string AuditAction => "Lesson.Unpublish";
    public string AuditResourceType => "Lesson";
    public Guid? AuditResourceId => LessonId;
}
