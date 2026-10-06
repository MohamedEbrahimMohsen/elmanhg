using Core.Errors;
using Core.OTP;
using Core.Messaging.Sms;
using Core.OTP.Delivery;
using Core.OTP.Delivery.Email;
using Core.OTP.Delivery.Sms;
using Core.OTP.Delivery.WhatsApp;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using NSubstitute;
using System.Net;

namespace Elmanhg.Tests.Core.Otp.Delivery;

public sealed class OtpDeliveryResilienceTests
{
    private const string Phone = "01012345678";
    private const string Code = "482913";

    private readonly StubHttpMessageHandler _handler = new() { StatusCode = HttpStatusCode.InternalServerError };

    [Fact]
    public async Task MetaWhatsApp_ServerError_MakesExactlyOneAttempt()
    {
        await SendAndExpectFailure<MetaWhatsAppOtpChannel>(OtpDeliveryTestSettings.WithMeta(), Phone, nameof(MetaWhatsAppOtpChannel));

        _handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task HttpSms_ServerError_MakesExactlyOneAttempt()
    {
        await SendAndExpectFailure<HttpSmsOtpChannel>(OtpDeliveryTestSettings.WithHttpSms(), Phone, nameof(HttpSmsClient));

        _handler.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ResendEmail_ServerError_RetriesWithTheSameIdempotencyKey()
    {
        await SendAndExpectFailure<ResendEmailOtpChannel>(OtpDeliveryTestSettings.WithResend(), "mona@elmanhg.test", nameof(ResendEmailOtpChannel));

        _handler.CallCount.Should().BeGreaterThan(1);
        _handler.RequestHeaders.Select(x => x["Idempotency-Key"]).Distinct().Should().ContainSingle().Which.Should().NotBeNullOrWhiteSpace();
    }

    private async Task SendAndExpectFailure<TChannel>(OtpDeliveryOptions options, string recipient, string clientName)
        where TChannel : class, IOtpChannel
    {
        using var provider = BuildProvider(options, clientName);
        using var scope = provider.CreateScope();
        var channel = scope.ServiceProvider.GetRequiredService<TChannel>();

        var act = () => channel.SendAsync(recipient, Code, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ServiceUnavailableCoreException>();
    }

    private ServiceProvider BuildProvider(OtpDeliveryOptions options, string clientName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(OtpDeliveryTestSettings.ToConfiguration(options)).Build());
        services.AddOptions<OtpOptions>();
        services.AddCoreOtpDelivery(OtpDeliveryTestSettings.Setup());
        services.AddHttpClient(clientName).ConfigurePrimaryHttpMessageHandler(() => _handler);
        services.PostConfigure<HttpStandardResilienceOptions>($"{clientName}-standard", resilience => resilience.Retry.Delay = TimeSpan.Zero);
        return services.BuildServiceProvider();
    }
}
