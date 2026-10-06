using Core.Errors;
using Core.Messaging.WhatsApp;
using Core.OTP.Delivery;
using Core.OTP.Delivery.WhatsApp;
using Elmanhg.Application.Exceptions;
using Elmanhg.Tests.Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Core.Otp.Delivery;

public sealed class MetaWhatsAppOtpChannelTests
{
    private const string Phone = "01012345678";
    private const string Code = "123456";

    private readonly StubHttpMessageHandler _handler = new();
    private readonly OtpDeliveryOptions _options = OtpDeliveryTestSettings.WithMeta();

    [Fact]
    public async Task SendAsync_ValidRecipient_PostsToGraphMessagesEndpointWithBearerToken()
    {
        await Channel().SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://graph.facebook.com/v23.0/123456/messages"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be("Bearer meta-token");
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public async Task SendAsync_ValidRecipient_SendsAuthenticationTemplateWithCode()
    {
        await Channel().SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        root.GetProperty("messaging_product").GetString().Should().Be("whatsapp");
        root.GetProperty("recipient_type").GetString().Should().Be("individual");
        root.GetProperty("to").GetString().Should().Be("201012345678");
        root.GetProperty("type").GetString().Should().Be("template");
        var template = root.GetProperty("template");
        template.GetProperty("name").GetString().Should().Be("elmanhg_otp");
        template.GetProperty("language").GetProperty("code").GetString().Should().Be("ar");
        var components = template.GetProperty("components");
        components[0].GetProperty("type").GetString().Should().Be("body");
        components[0].GetProperty("parameters")[0].GetProperty("text").GetString().Should().Be(Code);
        components[1].GetProperty("type").GetString().Should().Be("button");
        components[1].GetProperty("sub_type").GetString().Should().Be("url");
        components[1].GetProperty("index").GetString().Should().Be("0");
        components[1].GetProperty("parameters")[0].GetProperty("text").GetString().Should().Be(Code);
    }

    [Fact]
    public async Task SendAsync_CopyCodeButtonDisabled_SendsOnlyBodyComponent()
    {
        _options.WhatsApp.CopyCodeButton = false;

        await Channel().SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(_handler.LastBody!);
        body.RootElement.GetProperty("template").GetProperty("components").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SendAsync_ProviderRejectsToken_ThrowsDeliveryFailed()
    {
        _handler.StatusCode = HttpStatusCode.Unauthorized;

        var act = () => Channel().SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpDeliveryFailed);
    }

    [Fact]
    public async Task SendAsync_TransportFailure_ThrowsDeliveryFailed()
    {
        var failure = new HttpRequestException("connection refused");
        _handler.Throw = failure;

        var act = () => Channel().SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        var exception = (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.OtpDeliveryFailed);
        exception.InnerException.Should().BeSameAs(failure);
    }

    [Fact]
    public async Task SendAsync_CallerCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _handler.Throw = new OperationCanceledException(cancellation.Token);

        var act = () => Channel().SendAsync(Phone, Code, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private MetaWhatsAppOtpChannel Channel() => new(new MetaWhatsAppClient(new HttpClient(_handler) { BaseAddress = new Uri("https://graph.facebook.com/") }, CoreHttpTestSettings.Create()), Options.Create(_options), OtpDeliveryTestSettings.Setup(), NullLogger<MetaWhatsAppOtpChannel>.Instance);
}
