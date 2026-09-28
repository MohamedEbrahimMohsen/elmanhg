using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Units.CreateUnit;

public sealed record CreateUnitCommand(Guid SubjectId, string Name) : IRequest<CreateUnitResult>, IAuditableCommand
{
    public string AuditAction => "Unit.Create";
    public string AuditResourceType => "Unit";
    public Guid? AuditResourceId => null;
}
