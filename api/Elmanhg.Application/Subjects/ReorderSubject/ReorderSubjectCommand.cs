using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Subjects.ReorderSubject;

public sealed record ReorderSubjectCommand(Guid SubjectId, int Position) : IRequest, IAuditableCommand
{
    public string AuditAction => "Subject.Reorder";
    public string AuditResourceType => "Subject";
    public Guid? AuditResourceId => SubjectId;
}
