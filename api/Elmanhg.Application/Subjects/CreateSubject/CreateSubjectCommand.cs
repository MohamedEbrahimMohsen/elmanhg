using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Subjects.CreateSubject;

public sealed record CreateSubjectCommand(string Name) : IRequest<CreateSubjectResult>, IAuditableCommand
{
    public string AuditAction => "Subject.Create";
    public string AuditResourceType => "Subject";
    public Guid? AuditResourceId => null;
}
