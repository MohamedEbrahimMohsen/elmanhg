using MediatR;

namespace Elmanhg.Application.TrainingExports.GetDueTrainingExportIds;

public sealed record GetDueTrainingExportIdsQuery : IRequest<List<Guid>>;
