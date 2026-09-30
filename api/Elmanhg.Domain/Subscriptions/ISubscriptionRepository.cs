using Core.DDD.Repositories;

namespace Elmanhg.Domain.Subscriptions;

public interface ISubscriptionRepository : IRepository<Subscription>
{
    Task<List<PlanCount>> CountActiveByPlanAsync(CancellationToken cancellationToken);
    Task<long> GetMonthlyRecurringRevenueMinorAsync(CancellationToken cancellationToken);
}
