using Core.Errors;
using Core.Http;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Payments;

public sealed class FakePaymentGateway(IOptions<PaymentsOptions> paymentsOptions) : IPaymentGateway
{
    public bool SupportsSimulatedCompletion => IsAllowed;

    private bool IsAllowed => paymentsOptions.Value.AllowFakePayments;

    public Task<PaymentCheckout> StartCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken)
    {
        if (!IsAllowed)
        {
            throw new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable);
        }

        return Task.FromResult(new PaymentCheckout(HttpBaseAddress.Combine(paymentsOptions.Value.FakeCheckoutPath, request.PaymentId.ToString())));
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
