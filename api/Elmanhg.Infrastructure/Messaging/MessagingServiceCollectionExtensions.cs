using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Infrastructure.Invitations;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Infrastructure.OtpDelivery.WhatsApp;
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
        services.AddHttpClient<MetaWhatsAppMessageChannel>((serviceProvider, client) => client.BaseAddress = OtpDeliveryServiceCollectionExtensions.BaseAddress(OtpDeliveryServiceCollectionExtensions.Options(serviceProvider).WhatsApp.BaseUrl)).AddOtpResilience(retryUnsafeMethods: false);
        services.AddHttpClient<ResendEmailMessageChannel>((serviceProvider, client) => client.BaseAddress = OtpDeliveryServiceCollectionExtensions.BaseAddress(OtpDeliveryServiceCollectionExtensions.Options(serviceProvider).Email.BaseUrl)).AddOtpResilience(retryUnsafeMethods: true);
        services.AddScoped<IMessageChannel>(serviceProvider => UsesMeta(OtpDeliveryServiceCollectionExtensions.Options(serviceProvider).WhatsApp, ReminderOptions(serviceProvider)) ? serviceProvider.GetRequiredService<MetaWhatsAppMessageChannel>() : Fake(serviceProvider, MessageChannel.WhatsApp));
        services.AddScoped<IMessageChannel>(serviceProvider => InvitationEmailServiceCollectionExtensions.UsesResend(OtpDeliveryServiceCollectionExtensions.Options(serviceProvider).Email) ? serviceProvider.GetRequiredService<ResendEmailMessageChannel>() : Fake(serviceProvider, MessageChannel.Email));
        return services;
    }

    public static bool UsesMeta(WhatsAppOtpOptions whatsApp, OutOfAppReminderOptions reminders) => whatsApp is { Enabled: true, Provider: WhatsAppProvider.Meta } && !string.IsNullOrWhiteSpace(reminders.WhatsAppTemplateName);

    private static OutOfAppReminderOptions ReminderOptions(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<OutOfAppReminderOptions>>().Value;

    private static FakeMessageChannel Fake(IServiceProvider serviceProvider, MessageChannel channel) => new(channel, serviceProvider.GetRequiredService<ILogger<FakeMessageChannel>>(), serviceProvider.GetRequiredService<IHostEnvironment>());
}
