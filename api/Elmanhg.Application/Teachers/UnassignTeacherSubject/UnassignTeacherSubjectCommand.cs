using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Teachers.UnassignTeacherSubject;

public sealed record UnassignTeacherSubjectCommand(Guid TeacherId, Guid SubjectId) : IRequest, IAuditableCommand
{
    public string AuditAction => "UnassignTeacherSubject";
    public string AuditResourceType => "Teacher";
    public Guid? AuditResourceId => TeacherId;
}
