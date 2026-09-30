using Core.OTP.Delivery;
using Elmanhg.Infrastructure.OtpDelivery.Email;
using Elmanhg.Infrastructure.OtpDelivery.Sms;
using Elmanhg.Infrastructure.OtpDelivery.WhatsApp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.OtpDelivery;

public static class OtpDeliveryServiceCollectionExtensions
{
    public static IServiceCollection AddOtpDelivery(this IServiceCollection services)
    {
        services.AddOptions<OtpDeliveryOptions>().BindConfiguration(OtpDeliveryOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<OtpDeliveryOptions>, OtpDeliveryOptionsValidator>();
        services.AddHttpClient<MetaWhatsAppOtpChannel>((serviceProvider, client) => client.BaseAddress = BaseAddress(Options(serviceProvider).WhatsApp.BaseUrl)).AddOtpResilience(retryUnsafeMethods: false);
        services.AddHttpClient<ResendEmailOtpChannel>((serviceProvider, client) => client.BaseAddress = BaseAddress(Options(serviceProvider).Email.BaseUrl)).AddOtpResilience(retryUnsafeMethods: true);
        services.AddHttpClient<HttpSmsOtpChannel>().AddOtpResilience(retryUnsafeMethods: false);
        services.AddScoped<IOtpChannel>(serviceProvider => Options(serviceProvider).WhatsApp switch
        {
            { Enabled: false } or { Provider: WhatsAppProvider.Fake } => Fake(serviceProvider, OtpChannel.WhatsApp),
            { Provider: WhatsAppProvider.Meta } => serviceProvider.GetRequiredService<MetaWhatsAppOtpChannel>(),
            _ => throw new InvalidOperationException("Unsupported OtpDelivery:WhatsApp:Provider."),
        });
        services.AddScoped<IOtpChannel>(serviceProvider => Options(serviceProvider).Email switch
        {
            { Enabled: false } or { Provider: EmailProvider.Fake } => Fake(serviceProvider, OtpChannel.Email),
            { Provider: EmailProvider.Resend } => serviceProvider.GetRequiredService<ResendEmailOtpChannel>(),
            _ => throw new InvalidOperationException("Unsupported OtpDelivery:Email:Provider."),
        });
        services.AddScoped<IOtpChannel>(serviceProvider => Options(serviceProvider).Sms switch
        {
            { Enabled: false } or { Provider: SmsProvider.Fake } => Fake(serviceProvider, OtpChannel.Sms),
            { Provider: SmsProvider.Http } => serviceProvider.GetRequiredService<HttpSmsOtpChannel>(),
            _ => throw new InvalidOperationException("Unsupported OtpDelivery:Sms:Provider."),
        });
        services.AddScoped<IOtpSender, OtpChannelRouter>();
        return services;
    }

    internal static OtpDeliveryOptions Options(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<OtpDeliveryOptions>>().Value;

    internal static Uri BaseAddress(string url) => new(url.TrimEnd('/') + "/");

    private static FakeOtpChannel Fake(IServiceProvider serviceProvider, OtpChannel channel) => new(channel, serviceProvider.GetRequiredService<ILogger<FakeOtpChannel>>(), serviceProvider.GetRequiredService<IHostEnvironment>());

    internal static void AddOtpResilience(this IHttpClientBuilder builder, bool retryUnsafeMethods)
    {
        builder.AddStandardResilienceHandler().Configure((resilience, serviceProvider) =>
        {
            var delivery = Options(serviceProvider);
            resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(delivery.AttemptTimeoutSeconds);
            resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(delivery.TotalTimeoutSeconds);
            if (!retryUnsafeMethods)
            {
                resilience.Retry.DisableForUnsafeHttpMethods();
            }
        });
    }
}
