using Core.Settings;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.SlaCalendars;
using MediatR;

namespace Elmanhg.Application.TeacherThreads.GetTeacherReplyDeadline;

public sealed class GetTeacherReplyDeadlineHandler(IExamPeriodRepository examPeriodRepository, IRuntimeSettings runtimeSettings, TimeProvider timeProvider) : IRequestHandler<GetTeacherReplyDeadlineQuery, TeacherReplyDeadlineResult>
{
    public async Task<TeacherReplyDeadlineResult> Handle(GetTeacherReplyDeadlineQuery request, CancellationToken cancellationToken)
    {
        var policy = await TeacherThreadSlaPolicyLoader.LoadAsync(runtimeSettings, examPeriodRepository, cancellationToken).ConfigureAwait(false);
        var now = timeProvider.GetUtcNow();
        var schedule = policy.ScheduleFrom(now);
        return new TeacherReplyDeadlineResult((int)policy.ReplySla.TotalHours, schedule.SlaDueAt, schedule.SlaDueAt > now + policy.ReplySla);
    }
}
