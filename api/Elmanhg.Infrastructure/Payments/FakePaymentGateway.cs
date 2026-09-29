using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Payments;

public sealed class FakePaymentGateway(IOptions<PaymentsOptions> paymentsOptions, IHostEnvironment hostEnvironment) : IPaymentGateway
{
    public bool SupportsSimulatedCompletion => IsAllowed;

    private bool IsAllowed => !hostEnvironment.IsProduction() && (hostEnvironment.IsDevelopment() || paymentsOptions.Value.AllowFakePayments);

    public Task<PaymentCheckout> StartCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (!IsAllowed)
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable);
        }

        return Task.FromResult(new PaymentCheckout($"{paymentsOptions.Value.FakeCheckoutPath.TrimEnd('/')}/{request.PaymentId}"));
    }

    public Task<PaymentRefund> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken)
    {
        if (!IsAllowed)
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable);
        }

        return Task.FromResult(new PaymentRefund($"fake-refund-{request.PaymentId:N}"));
    }
}
