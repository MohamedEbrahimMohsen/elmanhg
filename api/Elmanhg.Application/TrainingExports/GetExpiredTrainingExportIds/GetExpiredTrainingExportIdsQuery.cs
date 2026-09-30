using MediatR;

namespace Elmanhg.Application.TrainingExports.GetExpiredTrainingExportIds;

public sealed record GetExpiredTrainingExportIdsQuery(IReadOnlyCollection<Guid> ExcludedIds) : IRequest<List<Guid>>;
