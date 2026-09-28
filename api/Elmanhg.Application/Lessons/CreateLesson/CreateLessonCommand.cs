using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Lessons.CreateLesson;

public sealed record CreateLessonCommand(Guid UnitId, string Name) : IRequest<CreateLessonResult>, IAuditableCommand
{
    public string AuditAction => "Lesson.Create";
    public string AuditResourceType => "Lesson";
    public Guid? AuditResourceId => null;
}
