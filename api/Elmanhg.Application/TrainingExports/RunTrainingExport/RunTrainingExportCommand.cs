using MediatR;

namespace Elmanhg.Application.TrainingExports.RunTrainingExport;

public sealed record RunTrainingExportCommand(Guid ExportId) : IRequest;
