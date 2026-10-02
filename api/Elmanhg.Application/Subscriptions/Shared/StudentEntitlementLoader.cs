using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class StudentEntitlementLoader
{
    public static async Task<EntitlementResult> LoadAsync(ISubscriptionRepository subscriptionRepository, IRuntimeSettings runtimeSettings, Guid studentId, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var subscriptions = await subscriptionRepository.FindAsync(SubscriptionEntitlementSpecification.EntitledFor(studentId, now, options.GracePeriod), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var entitlement = StudentEntitlement.Resolve(subscriptions, now, options.GracePeriod);
        var limits = PlanLimits.From(await runtimeSettings.GetValuesAsync(cancellationToken).ConfigureAwait(false));
        return EntitlementResultGenerator.Generate(entitlement, options, limits, now);
    }
}
