using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Infrastructure.Payments;
using Elmanhg.Infrastructure.Payments.Paymob;
using Elmanhg.Tests.Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.Payments;

public sealed class PaymobPaymentGatewayRefundTests
{
    private readonly StubHttpMessageHandler _handler = new() { ResponseBody = "{\"id\":192036999,\"success\":true,\"pending\":false}" };
    private readonly PaymentsOptions _options = PaymentsTestSettings.WithPaymob();
    private readonly Guid _paymentId = Guid.NewGuid();

    [Fact]
    public async Task RefundAsync_ValidRequest_PostsToRefundEndpointWithTokenAuth()
    {
        await RefundAsync();

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://accept.paymob.com/api/acceptance/void_refund/refund"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be("Token sk_test");
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public async Task RefundAsync_ValidRequest_SendsTransactionIdAndAmount()
    {
        await RefundAsync();

        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        (root.GetProperty("transaction_id").ValueKind, root.GetProperty("transaction_id").GetString(), root.GetProperty("amount_cents").GetInt64()).Should().Be((JsonValueKind.String, "192036465", 19900L));
    }

    [Fact]
    public async Task RefundAsync_Approved_ReturnsRefundTransactionId()
    {
        var refund = await RefundAsync();

        refund.TransactionId.Should().Be("192036999");
    }

    [Fact]
    public async Task RefundAsync_NotSuccessful_ThrowsRefundDeclined()
    {
        _handler.ResponseBody = "{\"id\":192036999,\"success\":false,\"pending\":false}";

        await ExpectDeclinedAsync();
    }

    [Fact]
    public async Task RefundAsync_NotSuccessfulWithoutId_ThrowsRefundDeclined()
    {
        _handler.ResponseBody = "{\"success\":false,\"pending\":false}";

        await ExpectDeclinedAsync();
    }

    [Fact]
    public async Task RefundAsync_PendingResponse_ThrowsRefundDeclined()
    {
        _handler.ResponseBody = "{\"id\":192036999,\"success\":true,\"pending\":true}";

        await ExpectDeclinedAsync();
    }

    [Fact]
    public async Task RefundAsync_ClientError_ThrowsRefundDeclined()
    {
        _handler.StatusCode = HttpStatusCode.BadRequest;

        await ExpectDeclinedAsync();
    }

    [Fact]
    public async Task RefundAsync_ServerError_ThrowsPaymentGatewayUnavailable()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task RefundAsync_TooManyRequests_ThrowsPaymentGatewayUnavailable()
    {
        _handler.StatusCode = HttpStatusCode.TooManyRequests;

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task RefundAsync_NetworkFailure_ThrowsPaymentGatewayUnavailable()
    {
        _handler.Throw = new HttpRequestException();

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task RefundAsync_MissingId_ThrowsPaymentGatewayUnavailable()
    {
        _handler.ResponseBody = "{\"success\":true,\"pending\":false}";

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task RefundAsync_InvalidJsonBody_ThrowsPaymentGatewayUnavailable()
    {
        _handler.ResponseBody = "not-json";

        await ExpectUnavailableAsync();
    }

    private async Task ExpectDeclinedAsync()
    {
        var act = () => RefundAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentRefundDeclined);
    }

    private async Task ExpectUnavailableAsync()
    {
        var act = () => RefundAsync();

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentGatewayUnavailable);
    }

    private Task<PaymentRefund> RefundAsync()
    {
        var gateway = new PaymobPaymentGateway(new HttpClient(_handler) { BaseAddress = new Uri("https://accept.paymob.com/") }, Options.Create(_options), CoreHttpTestSettings.Create(), NullLogger<PaymobPaymentGateway>.Instance);
        return gateway.RefundAsync(new PaymentRefundRequest(_paymentId, "192036465", new Money(19900, "EGP")), TestContext.Current.CancellationToken);
    }
}
