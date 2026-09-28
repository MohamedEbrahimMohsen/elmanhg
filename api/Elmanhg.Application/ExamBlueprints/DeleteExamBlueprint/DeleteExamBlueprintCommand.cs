using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.ExamBlueprints.DeleteExamBlueprint;

public sealed record DeleteExamBlueprintCommand(Guid ExamBlueprintId) : IRequest, IAuditableCommand
{
    public string AuditAction => "ExamBlueprint.Delete";
    public string AuditResourceType => "ExamBlueprint";
    public Guid? AuditResourceId => ExamBlueprintId;
}
