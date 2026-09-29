using Elmanhg.Application.Subscriptions.ProcessPaymentNotification;
using Elmanhg.Application.Subscriptions.Shared;
using MediatR;

namespace Elmanhg.Application.Shared.Observability;

public sealed class PaymentNotificationMetricsBehaviour(ElmanhgMetrics metrics) : IPipelineBehavior<ProcessPaymentNotificationCommand, PaymentNotificationResult>
{
    public async Task<PaymentNotificationResult> Handle(ProcessPaymentNotificationCommand request, RequestHandlerDelegate<PaymentNotificationResult> next, CancellationToken cancellationToken)
    {
        var result = await next(cancellationToken).ConfigureAwait(false);
        metrics.RecordPaymentNotification(result.Outcome);
        return result;
    }
}
