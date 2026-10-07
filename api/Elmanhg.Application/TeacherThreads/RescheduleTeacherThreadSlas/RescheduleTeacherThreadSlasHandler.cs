using Core.Settings;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Domain.TeacherThreads;
using MediatR;

namespace Elmanhg.Application.TeacherThreads.RescheduleTeacherThreadSlas;

public sealed class RescheduleTeacherThreadSlasHandler(ITeacherThreadRepository teacherThreadRepository, IExamPeriodRepository examPeriodRepository, IRuntimeSettings runtimeSettings, TimeProvider timeProvider) : IRequestHandler<RescheduleTeacherThreadSlasCommand, int>
{
    public async Task<int> Handle(RescheduleTeacherThreadSlasCommand request, CancellationToken cancellationToken)
    {
        var policy = await TeacherThreadSlaPolicyLoader.LoadAsync(runtimeSettings, examPeriodRepository, cancellationToken).ConfigureAwait(false);
        var fingerprint = policy.Fingerprint;
        var page = await teacherThreadRepository.FindPaginatedAsync(1, request.BatchSize, cancellationToken, filter: x => x.Status == TeacherThreadStatus.Open && x.SlaScheduleFingerprint != fingerprint, orderBy: query => query.OrderBy(x => x.SlaWindowStartedAt).ThenBy(x => x.Id)).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var rescheduled = 0;
        foreach (var thread in page.Items)
        {
            if (thread.RescheduleSla(policy, now))
            {
                rescheduled++;
            }
        }

        if (rescheduled > 0)
        {
            await teacherThreadRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return rescheduled;
    }
}
