using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.MathStepGrading;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.MathStepGrading.GetDueMathStepGradeIds;

public sealed class GetDueMathStepGradeIdsHandler(IMathStepGradeRepository mathStepGradeRepository, IOptions<MathStepGradingOptions> mathStepGradingOptions, TimeProvider timeProvider) : IRequestHandler<GetDueMathStepGradeIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetDueMathStepGradeIdsQuery request, CancellationToken cancellationToken)
    {
        return await mathStepGradeRepository.GetDueIdsAsync(timeProvider.GetUtcNow(), mathStepGradingOptions.Value.SweepBatchSize, cancellationToken).ConfigureAwait(false);
    }
}
