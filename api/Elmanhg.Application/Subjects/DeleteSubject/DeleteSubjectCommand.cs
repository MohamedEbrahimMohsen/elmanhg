using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Subjects.DeleteSubject;

public sealed record DeleteSubjectCommand(Guid SubjectId) : IRequest, IAuditableCommand
{
    public string AuditAction => "Subject.Delete";
    public string AuditResourceType => "Subject";
    public Guid? AuditResourceId => SubjectId;
}
