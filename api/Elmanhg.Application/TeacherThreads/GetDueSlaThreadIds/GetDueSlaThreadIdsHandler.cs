using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherThreads.GetDueSlaThreadIds;

public sealed class GetDueSlaThreadIdsHandler(ITeacherThreadRepository teacherThreadRepository, IOptions<AskTeacherOptions> askTeacherOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider) : IRequestHandler<GetDueSlaThreadIdsQuery, List<Guid>>
{
    public async Task<List<Guid>> Handle(GetDueSlaThreadIdsQuery request, CancellationToken cancellationToken)
    {
        var askTeacher = askTeacherOptions.Value;
        var subscriptions = subscriptionsOptions.Value;
        return await teacherThreadRepository.GetSlaDueIdsAsync(timeProvider.GetUtcNow(), TimeSpan.FromHours(subscriptions.AskTeacherReplySlaHours), TimeSpan.FromHours(askTeacher.FirstReminderAfterHours), TimeSpan.FromHours(askTeacher.SecondReminderAfterHours), request.ExcludedIds, askTeacher.SlaSweepBatchSize, cancellationToken).ConfigureAwait(false);
    }
}
