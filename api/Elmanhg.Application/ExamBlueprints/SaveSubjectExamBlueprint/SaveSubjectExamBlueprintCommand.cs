using Core.Auditing;
using Elmanhg.Application.ExamBlueprints.Shared;
using MediatR;

namespace Elmanhg.Application.ExamBlueprints.SaveSubjectExamBlueprint;

public sealed record SaveSubjectExamBlueprintCommand(Guid SubjectId, ExamBlueprintInput Blueprint) : IRequest<ExamBlueprintResult>, IAuditableCommand
{
    public string AuditAction => "ExamBlueprint.SaveSubjectDefault";
    public string AuditResourceType => "ExamBlueprint";
    public Guid? AuditResourceId => null;
}
