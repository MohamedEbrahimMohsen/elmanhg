using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Infrastructure.Payments;
using Elmanhg.Infrastructure.Payments.Paymob;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Infrastructure.Payments;

public sealed class PaymobPaymentGatewayTests
{
    private readonly StubHttpMessageHandler _handler = new() { ResponseBody = "{\"id\":\"pi_1\",\"client_secret\":\"egy_csk_test_1\"}" };
    private readonly PaymentsOptions _options = PaymentsTestSettings.WithPaymob();
    private readonly Guid _paymentId = Guid.NewGuid();

    [Fact]
    public async Task StartCheckoutAsync_ValidRequest_PostsToIntentionEndpointWithTokenAuth()
    {
        await StartAsync();

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://accept.paymob.com/v1/intention/"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be("Token sk_test");
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public async Task StartCheckoutAsync_ValidRequest_SendsAmountMethodsItemsAndReferences()
    {
        await StartAsync();

        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        (root.GetProperty("amount").GetInt64(), root.GetProperty("currency").GetString()).Should().Be((19900L, "EGP"));
        root.GetProperty("payment_methods").EnumerateArray().Select(x => x.GetInt32()).Should().Equal(111, 222);
        var item = root.GetProperty("items")[0];
        (item.GetProperty("name").GetString(), item.GetProperty("amount").GetInt64(), item.GetProperty("quantity").GetInt32()).Should().Be(("Elmanhg Base Monthly", 19900L, 1));
        root.GetProperty("special_reference").GetString().Should().Be(_paymentId.ToString());
        root.GetProperty("notification_url").GetString().Should().Be("https://api.test/api/payments/paymob/webhook");
        root.GetProperty("redirection_url").GetString().Should().Be($"https://app.test/student/checkout-result/{_paymentId}");
    }

    [Fact]
    public async Task StartCheckoutAsync_ValidRequest_SendsBillingDataFromCustomer()
    {
        await StartAsync();

        var billing = BillingData();
        (billing.GetProperty("first_name").GetString(), billing.GetProperty("last_name").GetString(), billing.GetProperty("phone_number").GetString()).Should().Be(("Mona", "Ali", "01012345678"));
        (billing.GetProperty("email").GetString(), billing.GetProperty("country").GetString(), billing.GetProperty("street").GetString()).Should().Be(("NA", "EG", "NA"));
    }

    [Fact]
    public async Task StartCheckoutAsync_SingleWordName_SendsNaLastName()
    {
        await StartAsync("Mona");

        BillingData().GetProperty("last_name").GetString().Should().Be("NA");
    }

    [Fact]
    public async Task StartCheckoutAsync_NotificationUrlBlank_OmitsNotificationUrl()
    {
        _options.Paymob.NotificationUrl = string.Empty;

        await StartAsync();

        using var body = JsonDocument.Parse(_handler.LastBody!);
        body.RootElement.TryGetProperty("notification_url", out _).Should().BeFalse();
    }

    [Fact]
    public async Task StartCheckoutAsync_Success_ReturnsUnifiedCheckoutUrl()
    {
        var checkout = await StartAsync();

        checkout.RedirectUrl.Should().Be("https://accept.paymob.com/unifiedcheckout/?publicKey=pk_test&clientSecret=egy_csk_test_1");
    }

    [Fact]
    public async Task StartCheckoutAsync_NumericIntentionId_ReturnsUnifiedCheckoutUrl()
    {
        _handler.ResponseBody = "{\"id\":1234567,\"client_secret\":\"egy_csk_test_2\"}";

        var checkout = await StartAsync();

        checkout.RedirectUrl.Should().Be("https://accept.paymob.com/unifiedcheckout/?publicKey=pk_test&clientSecret=egy_csk_test_2");
    }

    [Fact]
    public async Task StartCheckoutAsync_ServerError_ThrowsPaymentGatewayUnavailable()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task StartCheckoutAsync_NetworkFailure_ThrowsPaymentGatewayUnavailable()
    {
        _handler.Throw = new HttpRequestException();

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task StartCheckoutAsync_MissingClientSecret_ThrowsPaymentGatewayUnavailable()
    {
        _handler.ResponseBody = "{\"id\":\"pi_1\"}";

        await ExpectUnavailableAsync();
    }

    [Fact]
    public async Task StartCheckoutAsync_InvalidJsonBody_ThrowsPaymentGatewayUnavailable()
    {
        _handler.ResponseBody = "not-json";

        await ExpectUnavailableAsync();
    }

    private async Task ExpectUnavailableAsync()
    {
        var act = () => StartAsync();

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentGatewayUnavailable);
    }

    private JsonElement BillingData() => JsonDocument.Parse(_handler.LastBody!).RootElement.GetProperty("billing_data").Clone();

    private Task<PaymentCheckout> StartAsync(string displayName = "Mona Ali")
    {
        var gateway = new PaymobPaymentGateway(new HttpClient(_handler) { BaseAddress = new Uri("https://accept.paymob.com/") }, Options.Create(_options), NullLogger<PaymobPaymentGateway>.Instance);
        var request = new PaymentCheckoutRequest(_paymentId, new Money(19900, "EGP"), SubscriptionPlan.Base, BillingPeriod.Monthly, new PaymentCustomer(displayName, null, "01012345678"));
        return gateway.StartCheckoutAsync(request, TestContext.Current.CancellationToken);
    }
}
