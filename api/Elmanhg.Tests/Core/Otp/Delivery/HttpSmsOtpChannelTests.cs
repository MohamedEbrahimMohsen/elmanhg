using Core.Errors;
using Core.Messaging.Sms;
using Core.OTP.Delivery;
using Core.OTP.Delivery.Sms;
using Elmanhg.Application.Exceptions;
using Elmanhg.Tests.Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace Elmanhg.Tests.Core.Otp.Delivery;

public sealed class HttpSmsOtpChannelTests
{
    private const string Phone = "01012345678";
    private const string Code = "123456";

    private readonly StubHttpMessageHandler _handler = new();
    private readonly OtpDeliveryOptions _options = OtpDeliveryTestSettings.WithHttpSms();

    [Fact]
    public async Task SendAsync_JsonTemplate_PostsRenderedBodyWithAuthHeader()
    {
        await Channel().SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://sms.example.test/send"));
        _handler.LastRequest.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");
        _handler.LastBody.Should().Be("{\"to\":\"201012345678\",\"text\":\"رمز الدخول إلى المنهج: 123456\"}");
        _handler.LastRequest.Headers.GetValues("X-Api-Key").Single().Should().Be("sms-key");
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public async Task SendAsync_FormTemplate_PostsUrlEncodedBody()
    {
        _options.Sms.ContentType = HttpSmsBodyRenderer.FormContentType;
        _options.Sms.BodyTemplate = "to={phoneNumber}&msg={message}";
        _options.Sms.MessageTemplate = "Code: {code}";

        await Channel().SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Content!.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");
        _handler.LastBody.Should().Be("to=01012345678&msg=Code%3A%20123456");
    }

    [Fact]
    public async Task SendAsync_NoAuthHeaderConfigured_SendsNoCustomHeader()
    {
        _options.Sms.AuthHeaderName = string.Empty;
        _options.Sms.AuthHeaderValue = string.Empty;

        await Channel().SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Headers.Contains("X-Api-Key").Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_ProviderFails_ThrowsDeliveryFailed()
    {
        _handler.StatusCode = HttpStatusCode.InternalServerError;

        var act = () => Channel().SendAsync(Phone, Code, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpDeliveryFailed);
    }

    private HttpSmsOtpChannel Channel() => new(new HttpSmsClient(new HttpClient(_handler), CoreHttpTestSettings.Create()), Options.Create(_options), OtpDeliveryTestSettings.Setup(), NullLogger<HttpSmsOtpChannel>.Instance);
}
