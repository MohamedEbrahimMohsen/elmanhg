using MediatR;

namespace Elmanhg.Application.TrainingExports.FailTrainingExport;

public sealed record FailTrainingExportCommand(Guid ExportId, string ErrorCode) : IRequest;
