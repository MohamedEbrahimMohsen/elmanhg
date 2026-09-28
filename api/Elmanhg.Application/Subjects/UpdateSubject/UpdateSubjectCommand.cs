using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Subjects.UpdateSubject;

public sealed record UpdateSubjectCommand(Guid SubjectId, string Name) : IRequest, IAuditableCommand
{
    public string AuditAction => "Subject.Update";
    public string AuditResourceType => "Subject";
    public Guid? AuditResourceId => SubjectId;
}
