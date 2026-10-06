using Core.OTP.Delivery;
using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Infrastructure.Messaging;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Tests.Infrastructure.OtpDelivery;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure.Messaging;

public sealed class MessagingServiceCollectionExtensionsTests
{
    private const string TemplateName = "elmanhg_teacher_reminder";
    private const string ThreadLink = "https://site.test/teacher/thread";

    [Fact]
    public void AddMessaging_FakeProviders_ResolvesFakeForBothChannels()
    {
        using var provider = BuildProvider(OtpDeliveryTestSettings.Fake(), []);
        using var scope = provider.CreateScope();

        var channels = scope.ServiceProvider.GetServices<IMessageChannel>().ToList();

        channels.Should().AllBeOfType<FakeMessageChannel>();
        channels.Select(x => x.Channel).Should().Equal(MessageChannel.WhatsApp, MessageChannel.Email);
    }

    [Fact]
    public void AddMessaging_MetaWithReminderTemplate_ResolvesMetaChannel()
    {
        ChannelFor(OtpDeliveryTestSettings.WithMeta(), new() { ["OutOfAppReminders:WhatsAppTemplateName"] = TemplateName }, MessageChannel.WhatsApp).Should().BeOfType<MetaWhatsAppMessageChannel>();
    }

    [Fact]
    public void AddMessaging_MetaWithoutReminderTemplate_ResolvesFakeWhatsApp()
    {
        ChannelFor(OtpDeliveryTestSettings.WithMeta(), [], MessageChannel.WhatsApp).Should().BeOfType<FakeMessageChannel>();
    }

    [Fact]
    public void AddMessaging_ResendProvider_ResolvesResendChannel()
    {
        ChannelFor(OtpDeliveryTestSettings.WithResend(), new() { ["OutOfAppReminders:ThreadLinkBaseUrl"] = ThreadLink }, MessageChannel.Email).Should().BeOfType<ResendEmailMessageChannel>();
    }

    [Fact]
    public void AddMessaging_ResendWithoutThreadLink_FailsStartupValidation()
    {
        using var provider = BuildProvider(OtpDeliveryTestSettings.WithResend(), []);

        var act = () => provider.GetRequiredService<IStartupValidator>().Validate();

        act.Should().Throw<OptionsValidationException>().WithMessage("*OutOfAppReminders:ThreadLinkBaseUrl*");
    }

    private static IMessageChannel ChannelFor(OtpDeliveryOptions options, Dictionary<string, string?> reminders, MessageChannel channel)
    {
        using var provider = BuildProvider(options, reminders);
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetServices<IMessageChannel>().Single(x => x.Channel == channel);
    }

    private static ServiceProvider BuildProvider(OtpDeliveryOptions options, Dictionary<string, string?> reminders)
    {
        var configuration = OtpDeliveryTestSettings.ToConfiguration(options);
        foreach (var (key, value) in reminders)
        {
            configuration[key] = value;
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        services.AddOtpDelivery();
        services.AddMessaging();
        return services.BuildServiceProvider();
    }
}
