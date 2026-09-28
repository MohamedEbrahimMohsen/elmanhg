using Core.Auditing;
using Elmanhg.Domain.Lessons;
using MediatR;

namespace Elmanhg.Application.Lessons.UpdateLesson;

public sealed record UpdateLessonCommand(Guid LessonId, string Name, string? Explanation, string? Summary, string? VideoUrl, IList<LessonObjectiveContent> Objectives) : IRequest, IAuditableCommand
{
    public string AuditAction => "Lesson.Update";
    public string AuditResourceType => "Lesson";
    public Guid? AuditResourceId => LessonId;
}
