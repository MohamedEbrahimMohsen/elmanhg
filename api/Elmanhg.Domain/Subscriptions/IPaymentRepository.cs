using Core.DDD.Repositories;
using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Domain.Subscriptions;

public interface IPaymentRepository : IRepository<Payment>
{
    Task<PaymentTotals> GetTotalsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);
    Task<List<DailyTotal>> GetRevenueByDayAsync(MetricsWindow window, CancellationToken cancellationToken);
}
