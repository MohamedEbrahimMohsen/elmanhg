using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Analytics;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.GetFunnelMetrics;

public sealed class GetFunnelMetricsHandler(IFunnelEventRepository funnelEventRepository, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions) : IRequestHandler<GetFunnelMetricsQuery, FunnelMetricsResult>
{
    public async Task<FunnelMetricsResult> Handle(GetFunnelMetricsQuery request, CancellationToken cancellationToken)
    {
        var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), dashboardOptions.Value);
        var steps = await funnelEventRepository.CountVisitorsByStepAsync(window.Start, window.End, cancellationToken).ConfigureAwait(false);
        var timing = await funnelEventRepository.GetLandingToFirstAnswerTimingAsync(window.Start, window.End, cancellationToken).ConfigureAwait(false);

        return FunnelMetricsResultGenerator.Generate(window, steps, timing);
    }
}
