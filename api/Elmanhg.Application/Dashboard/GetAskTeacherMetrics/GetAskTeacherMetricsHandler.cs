using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetAskTeacherMetrics;

public sealed class GetAskTeacherMetricsHandler(ITeacherThreadRepository teacherThreadRepository, ITeacherThreadSlaEventRepository teacherThreadSlaEventRepository, ISubjectRepository subjectRepository, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions) : IRequestHandler<GetAskTeacherMetricsQuery, AskTeacherMetricsResult>
{
    public async Task<AskTeacherMetricsResult> Handle(GetAskTeacherMetricsQuery request, CancellationToken cancellationToken)
    {
        await DashboardSubjectGuard.EnsureExistsAsync(request.SubjectId, subjectRepository, cancellationToken).ConfigureAwait(false);
        var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), dashboardOptions.Value);
        var now = window.Now;
        var open = await teacherThreadRepository.CountAsync(cancellationToken, x => x.Status != TeacherThreadStatus.Closed && (request.SubjectId == null || x.SubjectId == request.SubjectId)).ConfigureAwait(false);
        var awaitingReply = await teacherThreadRepository.CountAsync(cancellationToken, x => x.Status == TeacherThreadStatus.Open && (request.SubjectId == null || x.SubjectId == request.SubjectId)).ConfigureAwait(false);
        var overdue = await teacherThreadRepository.CountAsync(cancellationToken, x => x.Status == TeacherThreadStatus.Open && x.SlaDueAt <= now && (request.SubjectId == null || x.SubjectId == request.SubjectId)).ConfigureAwait(false);
        var breaches = await teacherThreadSlaEventRepository.CountBreachesAsync(window.Start, window.End, request.SubjectId, cancellationToken).ConfigureAwait(false);
        var replies = await teacherThreadRepository.GetReplyStatsAsync(window.Start, window.End, request.SubjectId, null, cancellationToken).ConfigureAwait(false);

        return new AskTeacherMetricsResult(window.From, window.To, request.SubjectId, open, awaitingReply, overdue, breaches, replies.Replies, replies.RepliedWithinSla, DashboardRates.Ratio(replies.RepliedWithinSla, replies.Replies, DashboardRates.RateDecimals), DashboardRates.Seconds(replies.MedianReplySeconds), window.Now);
    }
}
