using Core.Errors;
using Core.Messaging.Email;
using Core.OTP;
using Core.OTP.Delivery.Email;
using Elmanhg.Application.Exceptions;
using Elmanhg.Tests.Core.Http;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace Elmanhg.Tests.Core.Otp.Delivery;

public sealed class ResendEmailOtpChannelTests
{
    private const string Email = "mona@elmanhg.test";
    private const string Code = "482913";

    private readonly StubHttpMessageHandler _handler = new();

    [Fact]
    public async Task SendAsync_ValidRecipient_PostsToEmailsWithBearerKeyAndIdempotencyKey()
    {
        await Channel().SendAsync(Email, Code, TestContext.Current.CancellationToken);

        _handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        _handler.LastRequest.RequestUri.Should().Be(new Uri("https://api.resend.com/emails"));
        _handler.LastRequest.Headers.Authorization!.ToString().Should().Be("Bearer re_test");
        _handler.LastRequest.Headers.GetValues("Idempotency-Key").Single().Should().NotBeNullOrWhiteSpace();
        _handler.LastRequest.Headers.UserAgent.ToString().Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public async Task SendAsync_ValidRecipient_SendsArabicHtmlAndTextWithCode()
    {
        await Channel().SendAsync(Email, Code, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(_handler.LastBody!);
        var root = body.RootElement;
        root.GetProperty("from").GetString().Should().Be("Elmanhg <otp@elmanhg.test>");
        root.GetProperty("to").EnumerateArray().Select(x => x.GetString()).Should().Equal(Email);
        root.GetProperty("subject").GetString().Should().Be("رمز الدخول إلى المنهج");
        root.GetProperty("html").GetString().Should().Contain("dir=\"rtl\"").And.Contain("lang=\"ar\"").And.Contain(Code).And.Contain("5");
        root.GetProperty("text").GetString().Should().Contain(Code);
    }

    [Fact]
    public async Task SendAsync_ProviderRejects_ThrowsDeliveryFailed()
    {
        _handler.StatusCode = HttpStatusCode.UnprocessableEntity;

        var act = () => Channel().SendAsync(Email, Code, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpDeliveryFailed);
    }

    private ResendEmailOtpChannel Channel() => new(new ResendEmailClient(new HttpClient(_handler) { BaseAddress = new Uri("https://api.resend.com/") }, CoreHttpTestSettings.Create()), Options.Create(OtpDeliveryTestSettings.WithResend()), Options.Create(new OtpOptions()), OtpDeliveryTestSettings.Setup(), NullLogger<ResendEmailOtpChannel>.Instance);
}
