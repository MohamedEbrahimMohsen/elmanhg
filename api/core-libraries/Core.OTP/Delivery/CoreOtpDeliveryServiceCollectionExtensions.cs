using Core.Http;
using Core.Messaging.Email;
using Core.Messaging.Sms;
using Core.Messaging.WhatsApp;
using Core.OTP.Delivery.Email;
using Core.OTP.Delivery.Sms;
using Core.OTP.Delivery.WhatsApp;
using Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.OTP.Delivery;

public static class CoreOtpDeliveryServiceCollectionExtensions
{
    public static IServiceCollection AddCoreOtpDelivery(this IServiceCollection services, OtpDeliverySetup setup)
    {
        services.AddValidatedOptions<OtpDeliveryOptions, OtpDeliveryOptionsValidator>(OtpDeliveryOptions.SectionName);
        services.AddSingleton(setup);
        services.AddScopedHttpConsumer<MetaWhatsAppOtpChannel, MetaWhatsAppClient>((serviceProvider, client) => client.BaseAddress = HttpBaseAddress.From(Options(serviceProvider).WhatsApp.BaseUrl)).AddOtpProviderResilience(retryUnsafeMethods: false);
        services.AddScopedHttpConsumer<ResendEmailOtpChannel, ResendEmailClient>((serviceProvider, client) => client.BaseAddress = HttpBaseAddress.From(Options(serviceProvider).Email.BaseUrl)).AddOtpProviderResilience(retryUnsafeMethods: true);
        services.AddHttpClient<HttpSmsClient>(nameof(HttpSmsOtpChannel)).AddOtpProviderResilience(retryUnsafeMethods: false);
        services.AddScoped<HttpSmsOtpChannel>();
        services.AddProviderSwitch<IOtpChannel, MetaWhatsAppOtpChannel>(serviceProvider => OtpDeliveryProviders.UsesMeta(Options(serviceProvider).WhatsApp), serviceProvider => Fake(serviceProvider, OtpChannel.WhatsApp));
        services.AddProviderSwitch<IOtpChannel, ResendEmailOtpChannel>(serviceProvider => OtpDeliveryProviders.UsesResend(Options(serviceProvider).Email), serviceProvider => Fake(serviceProvider, OtpChannel.Email));
        services.AddProviderSwitch<IOtpChannel, HttpSmsOtpChannel>(serviceProvider => OtpDeliveryProviders.UsesHttp(Options(serviceProvider).Sms), serviceProvider => Fake(serviceProvider, OtpChannel.Sms));
        services.AddScoped<IOtpSender, OtpChannelRouter>();
        return services;
    }

    public static IHttpStandardResiliencePipelineBuilder AddOtpProviderResilience(this IHttpClientBuilder builder, bool retryUnsafeMethods) => builder.AddTimeoutResilience(serviceProvider => TimeSpan.FromSeconds(Options(serviceProvider).AttemptTimeoutSeconds), serviceProvider => TimeSpan.FromSeconds(Options(serviceProvider).TotalTimeoutSeconds), retryUnsafeMethods);

    private static OtpDeliveryOptions Options(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<OtpDeliveryOptions>>().Value;

    private static FakeOtpChannel Fake(IServiceProvider serviceProvider, OtpChannel channel) => new(channel, serviceProvider.GetRequiredService<ILogger<FakeOtpChannel>>(), serviceProvider.GetRequiredService<IHostEnvironment>());
}
