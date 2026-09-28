using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Units.UpdateUnit;

public sealed record UpdateUnitCommand(Guid SubjectId, Guid UnitId, string Name) : IRequest, IAuditableCommand
{
    public string AuditAction => "Unit.Update";
    public string AuditResourceType => "Unit";
    public Guid? AuditResourceId => UnitId;
}
