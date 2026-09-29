using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Infrastructure.Payments;
using Elmanhg.Infrastructure.Payments.Paymob;
using Elmanhg.Tests.Fixtures.Paymob;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace Elmanhg.Tests.Infrastructure.Payments;

public sealed class PaymobNotificationReaderTests
{
    private const string Secret = "hmac_test";
    private readonly PaymentsOptions _options = PaymentsTestSettings.WithPaymob();
    private readonly Guid _paymentId = Guid.NewGuid();

    [Fact]
    public void Read_SignedTransaction_MapsFields()
    {
        var payload = Payload(PaymobPayloads.Succeeded);

        var notification = Reader().Read(payload, PaymobPayloads.Sign(payload, Secret));

        notification.Should().Be(new PaymentNotification("555", _paymentId.ToString(), "777", true, false, false, 19900, "EGP"));
    }

    [Fact]
    public void Read_TokenCallback_ReturnsNull()
    {
        var notification = Reader().Read(PaymobPayloads.Read(PaymobPayloads.Token), null);

        notification.Should().BeNull();
    }

    [Fact]
    public void Read_BadSignature_ThrowsSignatureInvalid()
    {
        var payload = Payload(PaymobPayloads.Succeeded);

        var act = () => Reader().Read(payload, PaymobPayloads.Sign(payload, "another-secret"));

        act.Should().Throw<UnauthorizedCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymobWebhookSignatureInvalid);
    }

    [Fact]
    public void Read_EmptyHmacSecret_ThrowsSignatureInvalid()
    {
        _options.Paymob.HmacSecret = string.Empty;
        var payload = Payload(PaymobPayloads.Succeeded);

        var act = () => Reader().Read(payload, PaymobPayloads.Sign(payload, string.Empty));

        act.Should().Throw<UnauthorizedCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymobWebhookSignatureInvalid);
    }

    [Fact]
    public void Read_SignatureOnlyInBody_IsAccepted()
    {
        var root = JsonNode.Parse(Payload(PaymobPayloads.Succeeded))!;
        root["hmac"] = PaymobPayloads.Sign(root.ToJsonString(), Secret);

        var notification = Reader().Read(root.ToJsonString(), null);

        notification!.TransactionId.Should().Be("555");
    }

    [Fact]
    public void Read_MalformedJson_ThrowsPayloadInvalid()
    {
        var act = () => Reader().Read("{\"type\":", "abc");

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymobWebhookPayloadInvalid);
    }

    [Fact]
    public void Read_SignedWithoutOrder_ThrowsPayloadInvalid()
    {
        var root = JsonNode.Parse(Payload(PaymobPayloads.Succeeded))!;
        root["obj"]!.AsObject().Remove("order");
        var payload = root.ToJsonString();

        var act = () => Reader().Read(payload, PaymobPayloads.Sign(payload, Secret));

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymobWebhookPayloadInvalid);
    }

    [Fact]
    public void Read_RefundedTransaction_FlagsRefundOrVoid()
    {
        var payload = Payload(PaymobPayloads.Refunded);

        var notification = Reader().Read(payload, PaymobPayloads.Sign(payload, Secret));

        (notification!.IsRefundOrVoid, notification.Succeeded).Should().Be((true, true));
    }

    private PaymobNotificationReader Reader() => new(Options.Create(_options));

    private string Payload(string fileName) => PaymobPayloads.ForPayment(fileName, _paymentId, 555, 19900, 777);
}
