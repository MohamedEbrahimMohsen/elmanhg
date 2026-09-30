using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Students.Shared;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.GrantComplimentarySubscription;

public sealed class GrantComplimentarySubscriptionHandler(IUserRepository userRepository, ISubscriptionRepository subscriptionRepository, ICurrentUserService currentUserService, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider) : IRequestHandler<GrantComplimentarySubscriptionCommand, AdminSubscriptionResult>
{
    public async Task<AdminSubscriptionResult> Handle(GrantComplimentarySubscriptionCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var student = await StudentLookup.GetAsync(userRepository, request.StudentId, cancellationToken).ConfigureAwait(false);
        var options = subscriptionsOptions.Value;
        var price = options.PriceFor(request.Plan, request.Period) ?? throw new BadRequestCoreException(ErrorCodes.ComplimentaryPeriodUnavailable);
        var now = timeProvider.GetUtcNow();
        var held = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledFor(student.Id, now, options.GracePeriod), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        StudentEntitlement.Resolve(held, now, options.GracePeriod).EnsureCanGrant(request.Plan);

        var subscription = Subscription.Start(student.Id, request.Plan, request.Period, price.Months, now, paymobReference: null, createdBy: currentUserService.UserId.Value);
        await subscriptionRepository.AddAsync(subscription, cancellationToken).ConfigureAwait(false);
        await subscriptionRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminSubscriptionResultGenerator.Generate(subscription, options.GracePeriod);
    }
}
