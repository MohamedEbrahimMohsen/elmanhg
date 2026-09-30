using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.TrainingExports;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TrainingExports.GetExpiredTrainingExportIds;

public sealed class GetExpiredTrainingExportIdsHandler(ITrainingExportRepository trainingExportRepository, IOptions<TrainingExportsOptions> trainingExportsOptions, TimeProvider timeProvider) : IRequestHandler<GetExpiredTrainingExportIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetExpiredTrainingExportIdsQuery request, CancellationToken cancellationToken)
    {
        return await trainingExportRepository.GetExpiredIdsAsync(timeProvider.GetUtcNow(), trainingExportsOptions.Value.RetentionSweepBatchSize, request.ExcludedIds, cancellationToken).ConfigureAwait(false);
    }
}
