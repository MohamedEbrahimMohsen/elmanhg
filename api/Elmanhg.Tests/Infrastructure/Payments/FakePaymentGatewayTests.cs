using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Payments;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.Payments;

public sealed class FakePaymentGatewayTests
{
    private readonly IHostEnvironment _hostEnvironment = Substitute.For<IHostEnvironment>();
    private readonly Guid _paymentId = Guid.NewGuid();

    [Fact]
    public async Task StartCheckoutAsync_Development_ReturnsFakeCheckoutPathForPayment()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        var checkout = await Gateway().StartCheckoutAsync(Request(), TestContext.Current.CancellationToken);

        checkout.RedirectUrl.Should().Be($"/student/fake-checkout/{_paymentId}");
    }

    [Fact]
    public async Task StartCheckoutAsync_Production_ThrowsPaymentGatewayUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        var act = () => Gateway().StartCheckoutAsync(Request(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentGatewayUnavailable);
    }

    [Fact]
    public void SupportsSimulatedCompletion_Production_IsFalse()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        Gateway().SupportsSimulatedCompletion.Should().BeFalse();
    }

    [Fact]
    public void SupportsSimulatedCompletion_Development_IsTrue()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        Gateway().SupportsSimulatedCompletion.Should().BeTrue();
    }

    [Fact]
    public void SupportsSimulatedCompletion_StagingWithAllowFakePayments_IsTrue()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Staging);

        Gateway(allowFakePayments: true).SupportsSimulatedCompletion.Should().BeTrue();
    }

    [Fact]
    public void SupportsSimulatedCompletion_StagingWithoutAllowFakePayments_IsFalse()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Staging);

        Gateway().SupportsSimulatedCompletion.Should().BeFalse();
    }

    [Fact]
    public void SupportsSimulatedCompletion_ProductionWithAllowFakePayments_IsFalse()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        Gateway(allowFakePayments: true).SupportsSimulatedCompletion.Should().BeFalse();
    }

    [Fact]
    public async Task StartCheckoutAsync_StagingWithoutAllowFakePayments_ThrowsPaymentGatewayUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Staging);

        var act = () => Gateway().StartCheckoutAsync(Request(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentGatewayUnavailable);
    }

    [Fact]
    public async Task StartCheckoutAsync_StagingWithAllowFakePayments_ReturnsFakeCheckoutPathForPayment()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Staging);

        var checkout = await Gateway(allowFakePayments: true).StartCheckoutAsync(Request(), TestContext.Current.CancellationToken);

        checkout.RedirectUrl.Should().Be($"/student/fake-checkout/{_paymentId}");
    }

    [Fact]
    public async Task RefundAsync_Development_ReturnsFakeRefundTransaction()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Development);

        var refund = await Gateway().RefundAsync(RefundRequest(), TestContext.Current.CancellationToken);

        refund.TransactionId.Should().Be($"fake-refund-{_paymentId:N}");
    }

    [Fact]
    public async Task RefundAsync_Production_ThrowsPaymentGatewayUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Production);

        var act = () => Gateway().RefundAsync(RefundRequest(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentGatewayUnavailable);
    }

    [Fact]
    public async Task RefundAsync_StagingWithoutAllowFakePayments_ThrowsPaymentGatewayUnavailable()
    {
        _hostEnvironment.EnvironmentName.Returns(Environments.Staging);

        var act = () => Gateway().RefundAsync(RefundRequest(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentGatewayUnavailable);
    }

    private PaymentRefundRequest RefundRequest() => new(_paymentId, "txn-1", new Money(19900, "EGP"));

    private FakePaymentGateway Gateway(bool allowFakePayments = false)
    {
        var options = PaymentsTestSettings.Fake();
        options.AllowFakePayments = allowFakePayments;
        return new(Options.Create(options), _hostEnvironment);
    }

    private PaymentCheckoutRequest Request() => new(_paymentId, new Money(19900, "EGP"), SubscriptionPlan.Base, BillingPeriod.Monthly, new PaymentCustomer("Mona Ali", null, "01012345678"));
}
