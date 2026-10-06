using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.GetMyEntitlement;

public sealed class GetMyEntitlementHandler(ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, IRuntimeSettings runtimeSettings) : IRequestHandler<GetMyEntitlementQuery, EntitlementResult>
{
    public async Task<EntitlementResult> Handle(GetMyEntitlementQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        return await StudentEntitlementLoader.LoadAsync(subscriptionRepository, runtimeSettings, userId, subscriptionsOptions.Value, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
    }
}
