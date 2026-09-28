using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Lessons.ArchiveLesson;

public sealed record ArchiveLessonCommand(Guid LessonId) : IRequest, IAuditableCommand
{
    public string AuditAction => "Lesson.Archive";
    public string AuditResourceType => "Lesson";
    public Guid? AuditResourceId => LessonId;
}
