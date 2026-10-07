using Core.Identity.Tokens.CurrentUser;
using Core.Settings;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Avatar;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Avatar.GetAvatarStatus;

public sealed class GetAvatarStatusHandler(ISessionRepository sessionRepository, ISubscriptionRepository subscriptionRepository, IAvatarMessageUsageRepository avatarMessageUsageRepository, IOptions<AvatarOptions> avatarOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, IOptions<ExamsOptions> examsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, IRuntimeSettings runtimeSettings) : IRequestHandler<GetAvatarStatusQuery, AvatarStatusResult>
{
    public async Task<AvatarStatusResult> Handle(GetAvatarStatusQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);
        var now = timeProvider.GetUtcNow();
        var examInProgress = await AvatarGate.IsExamInProgressAsync(userId, sessionRepository, examsOptions.Value, now, cancellationToken).ConfigureAwait(false);
        var entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, subscriptionsOptions.Value, now, cancellationToken).ConfigureAwait(false);
        var used = await AvatarGate.CountMessagesTodayAsync(userId, avatarMessageUsageRepository, subscriptionsOptions.Value, now, cancellationToken).ConfigureAwait(false);
        var deletionEnabled = await runtimeSettings.GetAsync(FeatureFlagRuntimeSettings.StudentsCanDeleteAvatarChats, cancellationToken).ConfigureAwait(false);
        var limit = entitlement.DailyAvatarMessageLimit;
        return new AvatarStatusResult(examInProgress, entitlement.Tier, limit, used, Math.Max(0, limit - used), avatarOptions.Value.MessageMaxLength, avatarOptions.Value.MaxHistoryMessages, deletionEnabled);
    }
}
