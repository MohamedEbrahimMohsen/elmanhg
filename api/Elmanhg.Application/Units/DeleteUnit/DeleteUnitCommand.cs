using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Units.DeleteUnit;

public sealed record DeleteUnitCommand(Guid SubjectId, Guid UnitId) : IRequest, IAuditableCommand
{
    public string AuditAction => "Unit.Delete";
    public string AuditResourceType => "Unit";
    public Guid? AuditResourceId => UnitId;
}
