using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetPaymentMetrics;

public sealed class GetPaymentMetricsHandler(IPaymentRepository paymentRepository, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions, IOptions<SubscriptionsOptions> subscriptionsOptions) : IRequestHandler<GetPaymentMetricsQuery, PaymentMetricsResult>
{
    public async Task<PaymentMetricsResult> Handle(GetPaymentMetricsQuery request, CancellationToken cancellationToken)
    {
        var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), dashboardOptions.Value);
        var totals = await paymentRepository.GetTotalsAsync(window.Start, window.End, cancellationToken).ConfigureAwait(false);
        var daily = await paymentRepository.GetRevenueByDayAsync(window.ToMetricsWindow(), cancellationToken).ConfigureAwait(false);
        var currency = subscriptionsOptions.Value.Currency;

        return new PaymentMetricsResult(window.From, window.To, totals.Succeeded, totals.Failed, totals.Refunds, new Money(totals.GrossMinor, currency), new Money(totals.RefundedMinor, currency), new Money(totals.GrossMinor - totals.RefundedMinor, currency), DashboardSeries.Fill(window, daily), window.Now);
    }
}
