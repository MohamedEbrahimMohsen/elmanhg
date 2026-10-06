using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetMyTeacherStats;

public sealed class GetMyTeacherStatsHandler(IQuestionRepository questionRepository, ITeacherThreadRepository teacherThreadRepository, ICurrentUserService currentUserService, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions) : IRequestHandler<GetMyTeacherStatsQuery, MyTeacherStatsResult>
{
    public async Task<MyTeacherStatsResult> Handle(GetMyTeacherStatsQuery request, CancellationToken cancellationToken)
    {
        var teacherId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), dashboardOptions.Value);
        var decisions = await questionRepository.GetDecisionStatsAsync(window.Start, window.End, null, teacherId, cancellationToken).ConfigureAwait(false);
        var replies = await teacherThreadRepository.GetReplyStatsAsync(window.Start, window.End, null, teacherId, cancellationToken).ConfigureAwait(false);

        return new MyTeacherStatsResult(window.From, window.To, decisions.Approved, decisions.Rejected, DashboardRates.Seconds(decisions.MedianSecondsToDecision), replies.Replies, replies.RepliedWithinSla, DashboardRates.Ratio(replies.RepliedWithinSla, replies.Replies, DashboardRates.RateDecimals), DashboardRates.Seconds(replies.MedianReplySeconds), window.Now);
    }
}
