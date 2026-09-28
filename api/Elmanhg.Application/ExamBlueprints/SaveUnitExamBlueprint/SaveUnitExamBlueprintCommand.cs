using Core.Auditing;
using Elmanhg.Application.ExamBlueprints.Shared;
using MediatR;

namespace Elmanhg.Application.ExamBlueprints.SaveUnitExamBlueprint;

public sealed record SaveUnitExamBlueprintCommand(Guid UnitId, ExamBlueprintInput Blueprint) : IRequest<ExamBlueprintResult>, IAuditableCommand
{
    public string AuditAction => "ExamBlueprint.SaveUnit";
    public string AuditResourceType => "ExamBlueprint";
    public Guid? AuditResourceId => null;
}
