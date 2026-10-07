using Core.Messaging.Sms;
using Core.OTP;
using Core.OTP.Delivery;
using Core.OTP.Delivery.Email;
using Core.OTP.Delivery.WhatsApp;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Elmanhg.Tests.Core.Otp.Delivery;

public sealed class CoreOtpDeliveryRegistrationTests
{
    [Fact]
    public void AddCoreOtpDelivery_UnknownWhatsAppProvider_ThrowsUnsupportedProvider()
    {
        var configuration = OtpDeliveryTestSettings.ToConfiguration(OtpDeliveryTestSettings.Fake());
        configuration["OtpDelivery:WhatsApp:Provider"] = "7";
        using var provider = BuildProvider(configuration);
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetServices<IOtpChannel>().ToList();

        act.Should().Throw<InvalidOperationException>().WithMessage("*WhatsApp:Provider*");
    }

    [Theory]
    [InlineData("OtpDelivery:Email:Provider", "*Email:Provider*")]
    [InlineData("OtpDelivery:Sms:Provider", "*Sms:Provider*")]
    public void AddCoreOtpDelivery_UnknownEmailOrSmsProvider_ThrowsUnsupportedProvider(string key, string message)
    {
        var configuration = OtpDeliveryTestSettings.ToConfiguration(OtpDeliveryTestSettings.WithHttpSms());
        configuration[key] = "7";
        using var provider = BuildProvider(configuration);
        using var scope = provider.CreateScope();

        var act = () => scope.ServiceProvider.GetServices<IOtpChannel>().ToList();

        act.Should().Throw<InvalidOperationException>().WithMessage(message);
    }

    [Fact]
    public void AddCoreOtpDelivery_EnabledProviders_ResolveTransportTypedClients()
    {
        var options = OtpDeliveryTestSettings.WithMeta();
        var resend = OtpDeliveryTestSettings.WithResend();
        var sms = OtpDeliveryTestSettings.WithHttpSms();
        options.Email = resend.Email;
        options.Sms = sms.Sms;
        using var provider = BuildProvider(OtpDeliveryTestSettings.ToConfiguration(options));
        using var scope = provider.CreateScope();

        var whatsApp = scope.ServiceProvider.GetRequiredService<MetaWhatsAppOtpChannel>();
        var email = scope.ServiceProvider.GetRequiredService<ResendEmailOtpChannel>();
        var smsClient = scope.ServiceProvider.GetRequiredService<HttpSmsClient>();
        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        whatsApp.Should().NotBeNull();
        email.Should().NotBeNull();
        smsClient.Should().NotBeNull();
        factory.CreateClient(nameof(MetaWhatsAppOtpChannel)).BaseAddress.Should().Be(new Uri("https://graph.facebook.com/"));
        factory.CreateClient(nameof(ResendEmailOtpChannel)).BaseAddress.Should().Be(new Uri("https://api.resend.com/"));
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        services.AddOptions<OtpOptions>();
        services.AddCoreOtpDelivery(OtpDeliveryTestSettings.Setup());
        return services.BuildServiceProvider();
    }
}
