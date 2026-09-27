using Core.Auditing;
using Elmanhg.Application.Teachers.Shared;
using MediatR;

namespace Elmanhg.Application.Teachers.AssignTeacherSubject;

public sealed record AssignTeacherSubjectCommand(Guid TeacherId, Guid SubjectId) : IRequest<TeacherSubjectResult>, IAuditableCommand
{
    public string AuditAction => "AssignTeacherSubject";
    public string AuditResourceType => "Teacher";
    public Guid? AuditResourceId => TeacherId;
}
