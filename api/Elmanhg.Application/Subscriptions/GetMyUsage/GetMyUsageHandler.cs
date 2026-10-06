using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Application.TeacherThreads.Shared;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.GetMyUsage;

public sealed class GetMyUsageHandler(ISubscriptionRepository subscriptionRepository, ISessionRepository sessionRepository, ITeacherThreadRepository teacherThreadRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, IRuntimeSettings runtimeSettings) : IRequestHandler<GetMyUsageQuery, UsageResult>
{
    public async Task<UsageResult> Handle(GetMyUsageQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var now = timeProvider.GetUtcNow();
        var options = subscriptionsOptions.Value;
        var entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, options, now, cancellationToken).ConfigureAwait(false);
        var used = await FreeTierGate.CountQuizQuestionsTodayAsync(userId, sessionRepository, options, now, cancellationToken).ConfigureAwait(false);
        var askTeacherUsed = await AskTeacherGate.CountQuestionsThisMonthAsync(userId, teacherThreadRepository, options, now, cancellationToken).ConfigureAwait(false);
        return new UsageResult(entitlement.Tier, entitlement.HasAskTeacher, entitlement.DailyQuizQuestionLimit, used, entitlement.DailyQuizQuestionLimit is { } limit ? Math.Max(0, limit - used) : null, entitlement.DailyAvatarMessageLimit, entitlement.MonthlyAskTeacherQuestionLimit, askTeacherUsed, Math.Max(0, entitlement.MonthlyAskTeacherQuestionLimit - askTeacherUsed));
    }
}
