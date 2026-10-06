using Core.Http;
using Core.Messaging.Email;
using Core.Messaging.WhatsApp;
using Core.OTP.Delivery;
using Core.OTP.Delivery.WhatsApp;
using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Infrastructure.Invitations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services)
    {
        services.AddOptions<OutOfAppReminderOptions>().BindConfiguration(OutOfAppReminderOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<OutOfAppReminderOptions>, OutOfAppReminderOptionsValidator>();
        services.AddScopedHttpConsumer<MetaWhatsAppMessageChannel, MetaWhatsAppClient>((serviceProvider, client) => client.BaseAddress = HttpBaseAddress.From(OtpDelivery(serviceProvider).WhatsApp.BaseUrl)).AddOtpProviderResilience(retryUnsafeMethods: false);
        services.AddScopedHttpConsumer<ResendEmailMessageChannel, ResendEmailClient>((serviceProvider, client) => client.BaseAddress = HttpBaseAddress.From(OtpDelivery(serviceProvider).Email.BaseUrl)).AddOtpProviderResilience(retryUnsafeMethods: true);
        services.AddProviderSwitch<IMessageChannel, MetaWhatsAppMessageChannel>(serviceProvider => UsesMeta(OtpDelivery(serviceProvider).WhatsApp, ReminderOptions(serviceProvider)), serviceProvider => Fake(serviceProvider, MessageChannel.WhatsApp));
        services.AddProviderSwitch<IMessageChannel, ResendEmailMessageChannel>(serviceProvider => InvitationEmailServiceCollectionExtensions.UsesResend(OtpDelivery(serviceProvider).Email), serviceProvider => Fake(serviceProvider, MessageChannel.Email));
        return services;
    }

    public static bool UsesMeta(WhatsAppOtpOptions whatsApp, OutOfAppReminderOptions reminders) => whatsApp is { Enabled: true, Provider: WhatsAppProvider.Meta } && !string.IsNullOrWhiteSpace(reminders.WhatsAppTemplateName);

    private static OtpDeliveryOptions OtpDelivery(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<OtpDeliveryOptions>>().Value;

    private static OutOfAppReminderOptions ReminderOptions(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<OutOfAppReminderOptions>>().Value;

    private static FakeMessageChannel Fake(IServiceProvider serviceProvider, MessageChannel channel) => new(channel, serviceProvider.GetRequiredService<ILogger<FakeMessageChannel>>(), serviceProvider.GetRequiredService<IHostEnvironment>());
}
