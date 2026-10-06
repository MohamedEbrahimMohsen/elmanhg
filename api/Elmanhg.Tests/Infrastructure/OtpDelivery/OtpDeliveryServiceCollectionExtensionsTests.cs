using Core.OTP;
using Core.OTP.Delivery;
using Core.OTP.Delivery.Email;
using Core.OTP.Delivery.Sms;
using Core.OTP.Delivery.WhatsApp;
using Core.OTP.Entities;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.OtpDelivery;

public sealed class OtpDeliveryServiceCollectionExtensionsTests
{
    [Fact]
    public void AddOtpDelivery_FakeProviders_ResolvesAFakeForEveryChannel()
    {
        using var provider = BuildProvider(OtpDeliveryTestSettings.Fake());
        using var scope = provider.CreateScope();

        var channels = scope.ServiceProvider.GetServices<IOtpChannel>().ToList();

        channels.Should().AllBeOfType<FakeOtpChannel>();
        channels.Select(x => x.Channel).Should().BeEquivalentTo([OtpChannel.WhatsApp, OtpChannel.Sms, OtpChannel.Email]);
        scope.ServiceProvider.GetRequiredService<IOtpSender>().Should().BeOfType<OtpChannelRouter>();
    }

    [Fact]
    public void AddOtpDelivery_MetaProvider_ResolvesMetaWhatsAppChannel()
    {
        ChannelFor(OtpDeliveryTestSettings.WithMeta(), OtpChannel.WhatsApp).Should().BeOfType<MetaWhatsAppOtpChannel>();
    }

    [Fact]
    public void AddOtpDelivery_ResendProvider_ResolvesResendEmailChannel()
    {
        ChannelFor(OtpDeliveryTestSettings.WithResend(), OtpChannel.Email).Should().BeOfType<ResendEmailOtpChannel>();
    }

    [Fact]
    public void AddOtpDelivery_HttpSmsProvider_ResolvesHttpSmsChannel()
    {
        ChannelFor(OtpDeliveryTestSettings.WithHttpSms(), OtpChannel.Sms).Should().BeOfType<HttpSmsOtpChannel>();
    }

    [Fact]
    public void AddOtpDelivery_MetaEnabledWithoutCredentials_FailsStartupValidation()
    {
        var options = OtpDeliveryTestSettings.WithMeta();
        options.WhatsApp.AccessToken = string.Empty;
        using var provider = BuildProvider(options);

        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        act.Should().Throw<OptionsValidationException>().WithMessage("*AccessToken*");
    }

    [Fact]
    public async Task AddOtpDelivery_DisabledMetaWithBlankBaseUrl_ResolvesFakeAndStillSendsEmail()
    {
        var options = OtpDeliveryTestSettings.WithMeta();
        options.WhatsApp.Enabled = false;
        options.WhatsApp.BaseUrl = string.Empty;
        using var provider = BuildProvider(options);
        using var scope = provider.CreateScope();

        var channel = await scope.ServiceProvider.GetRequiredService<IOtpSender>().SendAsync(OtpRecipientType.Email, "mona@elmanhg.test", "482913", TestContext.Current.CancellationToken);

        channel.Should().Be(OtpChannel.Email);
        scope.ServiceProvider.GetServices<IOtpChannel>().Single(x => x.Channel == OtpChannel.WhatsApp).Should().BeOfType<FakeOtpChannel>();
    }

    [Fact]
    public void AddOtpDelivery_Registered_ExposesElmanhgMetricsAsObserver()
    {
        using var provider = BuildProvider(OtpDeliveryTestSettings.Fake());

        var observers = provider.GetServices<IOtpDeliveryObserver>().ToList();

        observers.Should().ContainSingle().Which.Should().BeSameAs(provider.GetRequiredService<ElmanhgMetrics>());
    }

    private static IOtpChannel ChannelFor(OtpDeliveryOptions options, OtpChannel channel)
    {
        using var provider = BuildProvider(options);
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetServices<IOtpChannel>().Single(x => x.Channel == channel);
    }

    private static ServiceProvider BuildProvider(OtpDeliveryOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(OtpDeliveryTestSettings.ToConfiguration(options)).Build());
        services.AddOptions<OtpOptions>();
        services.AddMetrics();
        services.AddSingleton<ElmanhgMetrics>();
        services.AddOtpDelivery();
        return services.BuildServiceProvider();
    }
}
