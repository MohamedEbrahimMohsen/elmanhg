using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Infrastructure.Payments;

public sealed class FakePaymentGatewayTests
{
    private readonly Guid _paymentId = Guid.NewGuid();

    [Fact]
    public async Task StartCheckoutAsync_AllowFakePayments_ReturnsFakeCheckoutPathForPayment()
    {
        var checkout = await Gateway(allowFakePayments: true).StartCheckoutAsync(Request(), TestContext.Current.CancellationToken);

        checkout.RedirectUrl.Should().Be($"/student/fake-checkout/{_paymentId}");
    }

    [Fact]
    public async Task StartCheckoutAsync_FakePaymentsNotAllowed_ThrowsPaymentGatewayUnavailable()
    {
        var act = () => Gateway(allowFakePayments: false).StartCheckoutAsync(Request(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentGatewayUnavailable);
    }

    [Fact]
    public void SupportsSimulatedCompletion_AllowFakePayments_IsTrue()
    {
        Gateway(allowFakePayments: true).SupportsSimulatedCompletion.Should().BeTrue();
    }

    [Fact]
    public void SupportsSimulatedCompletion_FakePaymentsNotAllowed_IsFalse()
    {
        Gateway(allowFakePayments: false).SupportsSimulatedCompletion.Should().BeFalse();
    }

    [Fact]
    public async Task RefundAsync_AllowFakePayments_ReturnsFakeRefundTransaction()
    {
        var refund = await Gateway(allowFakePayments: true).RefundAsync(RefundRequest(), TestContext.Current.CancellationToken);

        refund.TransactionId.Should().Be($"fake-refund-{_paymentId:N}");
    }

    [Fact]
    public async Task RefundAsync_FakePaymentsNotAllowed_ThrowsPaymentGatewayUnavailable()
    {
        var act = () => Gateway(allowFakePayments: false).RefundAsync(RefundRequest(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentGatewayUnavailable);
    }

    private PaymentRefundRequest RefundRequest() => new(_paymentId, "txn-1", new Money(19900, "EGP"));

    private FakePaymentGateway Gateway(bool allowFakePayments)
    {
        var options = PaymentsTestSettings.Fake();
        options.AllowFakePayments = allowFakePayments;
        return new(Options.Create(options));
    }

    private PaymentCheckoutRequest Request() => new(_paymentId, new Money(19900, "EGP"), SubscriptionPlan.Base, BillingPeriod.Monthly, new PaymentCustomer("Mona Ali", null, "01012345678"));
}
