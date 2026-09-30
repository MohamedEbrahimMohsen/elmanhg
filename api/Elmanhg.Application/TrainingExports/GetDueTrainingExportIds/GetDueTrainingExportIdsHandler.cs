using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.TrainingExports;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TrainingExports.GetDueTrainingExportIds;

public sealed class GetDueTrainingExportIdsHandler(ITrainingExportRepository trainingExportRepository, IOptions<TrainingExportsOptions> trainingExportsOptions, TimeProvider timeProvider) : IRequestHandler<GetDueTrainingExportIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetDueTrainingExportIdsQuery request, CancellationToken cancellationToken)
    {
        return await trainingExportRepository.GetDueIdsAsync(timeProvider.GetUtcNow(), trainingExportsOptions.Value.SweepBatchSize, cancellationToken).ConfigureAwait(false);
    }
}
