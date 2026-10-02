using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherThreads.GetDueSlaThreadIds;

public sealed class GetDueSlaThreadIdsHandler(ITeacherThreadRepository teacherThreadRepository, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider) : IRequestHandler<GetDueSlaThreadIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetDueSlaThreadIdsQuery request, CancellationToken cancellationToken)
    {
        return await teacherThreadRepository.GetSlaDueIdsAsync(timeProvider.GetUtcNow(), request.ExcludedIds, askTeacherOptions.Value.SlaSweepBatchSize, cancellationToken).ConfigureAwait(false);
    }
}
