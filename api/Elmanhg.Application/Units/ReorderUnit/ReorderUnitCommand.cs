using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Units.ReorderUnit;

public sealed record ReorderUnitCommand(Guid SubjectId, Guid UnitId, int Position) : IRequest, IAuditableCommand
{
    public string AuditAction => "Unit.Reorder";
    public string AuditResourceType => "Unit";
    public Guid? AuditResourceId => UnitId;
}
