namespace Elmanhg.Application.Shared.Payments;

public interface IPaymentGateway
{
    bool SupportsSimulatedCompletion { get; }

    Task<PaymentCheckout> StartCheckoutAsync(PaymentCheckoutRequest request, CancellationToken cancellationToken);
}
