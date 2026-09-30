using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetSubscriberMetrics;

public sealed class GetSubscriberMetricsHandler(ISubscriptionRepository subscriptionRepository, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions, IOptions<SubscriptionsOptions> subscriptionsOptions) : IRequestHandler<GetSubscriberMetricsQuery, SubscriberMetricsResult>
{
    public async Task<SubscriberMetricsResult> Handle(GetSubscriberMetricsQuery request, CancellationToken cancellationToken)
    {
        var options = dashboardOptions.Value;
        var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), options);
        var monthStart = window.StartOfDay(window.Today.AddDays(-(options.RecentMonthDays - 1)));
        var byPlan = await subscriptionRepository.CountActiveByPlanAsync(cancellationToken).ConfigureAwait(false);
        var churnedInRange = await subscriptionRepository.CountAsync(cancellationToken, SubscriptionChurnSpecification.ChurnedBetween(window.Start, window.End)).ConfigureAwait(false);
        var churnedThisMonth = await subscriptionRepository.CountAsync(cancellationToken, SubscriptionChurnSpecification.ChurnedBetween(monthStart, window.Now)).ConfigureAwait(false);
        var monthlyRecurringRevenue = await subscriptionRepository.GetMonthlyRecurringRevenueMinorAsync(cancellationToken).ConfigureAwait(false);
        var activeByPlan = Enum.GetValues<SubscriptionPlan>()
            .Select(plan => new PlanCountResult(plan, byPlan.FirstOrDefault(x => x.Plan == plan)?.Count ?? 0))
            .ToList();

        return new SubscriberMetricsResult(window.From, window.To, activeByPlan.Sum(x => x.Count), activeByPlan, churnedInRange, churnedThisMonth, new Money(monthlyRecurringRevenue, subscriptionsOptions.Value.Currency), window.Now);
    }
}
