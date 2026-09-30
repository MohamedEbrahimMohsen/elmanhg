using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.TrainingExports.DownloadTrainingExport;

public sealed record DownloadTrainingExportQuery(Guid ExportId) : IRequest<TrainingExportFileResult>, IAuditableCommand
{
    public string AuditAction => "TrainingExport.Download";
    public string AuditResourceType => "TrainingExport";
    public Guid? AuditResourceId => ExportId;
}
