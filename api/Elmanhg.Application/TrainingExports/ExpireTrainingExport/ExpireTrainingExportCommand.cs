using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.TrainingExports.ExpireTrainingExport;

public sealed record ExpireTrainingExportCommand(Guid ExportId) : IRequest, IAuditableCommand
{
    public string AuditAction => "TrainingExport.Expire";
    public string AuditResourceType => "TrainingExport";
    public Guid? AuditResourceId => ExportId;
}
