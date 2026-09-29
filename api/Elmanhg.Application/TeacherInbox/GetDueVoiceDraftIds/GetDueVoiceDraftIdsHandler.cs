using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherInbox.GetDueVoiceDraftIds;

public sealed class GetDueVoiceDraftIdsHandler(ITeacherVoiceDraftRepository teacherVoiceDraftRepository, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider) : IRequestHandler<GetDueVoiceDraftIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetDueVoiceDraftIdsQuery request, CancellationToken cancellationToken)
    {
        return await teacherVoiceDraftRepository.GetDueIdsAsync(timeProvider.GetUtcNow(), askTeacherOptions.Value.TranscriptionSweepBatchSize, cancellationToken).ConfigureAwait(false);
    }
}
