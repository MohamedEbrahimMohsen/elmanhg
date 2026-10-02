using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherThreads.GetDueSlaThreadIds;

public sealed class GetDueSlaThreadIdsHandler(ITeacherThreadRepository teacherThreadRepository, IOptions<AskTeacherOptions> askTeacherOptions, IRuntimeSettings runtimeSettings, TimeProvider timeProvider) : IRequestHandler<GetDueSlaThreadIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetDueSlaThreadIdsQuery request, CancellationToken cancellationToken)
    {
        var askTeacher = askTeacherOptions.Value;
        var values = await runtimeSettings.GetValuesAsync(cancellationToken).ConfigureAwait(false);
        return await teacherThreadRepository.GetSlaDueIdsAsync(timeProvider.GetUtcNow(), TimeSpan.FromHours(values.Get(AskTeacherRuntimeSettings.ReplySlaHours)), TimeSpan.FromHours(values.Get(AskTeacherRuntimeSettings.FirstReminderAfterHours)), TimeSpan.FromHours(values.Get(AskTeacherRuntimeSettings.SecondReminderAfterHours)), request.ExcludedIds, askTeacher.SlaSweepBatchSize, cancellationToken).ConfigureAwait(false);
    }
}
