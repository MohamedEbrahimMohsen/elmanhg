using Core.Auditing;
using Elmanhg.Application.TrainingExports.Shared;
using Elmanhg.Domain.TrainingExports;
using MediatR;

namespace Elmanhg.Application.TrainingExports.RequestTrainingExport;

public sealed record RequestTrainingExportCommand(TrainingExportSource Source, DateTimeOffset From, DateTimeOffset To, Guid? SubjectId) : IRequest<TrainingExportResult>, IAuditableCommand
{
    public string AuditAction => "TrainingExport.Request";
    public string AuditResourceType => "TrainingExport";
    public Guid? AuditResourceId => null;
}
