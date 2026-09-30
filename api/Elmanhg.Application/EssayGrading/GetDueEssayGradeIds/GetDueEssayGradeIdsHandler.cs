using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.EssayGrading;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.EssayGrading.GetDueEssayGradeIds;

public sealed class GetDueEssayGradeIdsHandler(IEssayGradeRepository essayGradeRepository, IOptions<EssayGradingOptions> essayGradingOptions, TimeProvider timeProvider) : IRequestHandler<GetDueEssayGradeIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetDueEssayGradeIdsQuery request, CancellationToken cancellationToken)
    {
        return await essayGradeRepository.GetDueIdsAsync(timeProvider.GetUtcNow(), essayGradingOptions.Value.SweepBatchSize, cancellationToken).ConfigureAwait(false);
    }
}
