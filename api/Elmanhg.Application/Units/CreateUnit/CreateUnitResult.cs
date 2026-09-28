using Core.Auditing;

namespace Elmanhg.Application.Units.CreateUnit;

public sealed record CreateUnitResult(Guid Id) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => Id;
}
