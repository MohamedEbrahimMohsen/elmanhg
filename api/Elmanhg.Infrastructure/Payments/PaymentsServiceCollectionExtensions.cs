using Core.Http;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Infrastructure.Payments.Paymob;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Payments;

public static class PaymentsServiceCollectionExtensions
{
    public static IServiceCollection AddPayments(this IServiceCollection services)
    {
        services.AddOptions<PaymentsOptions>().BindConfiguration(PaymentsOptions.SectionName).PostConfigure<IConfiguration, IHostEnvironment>(ApplyAllowFakePaymentsDefault).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<PaymentsOptions>, PaymentsOptionsValidator>();
        services.AddHttpClient<PaymobPaymentGateway>((serviceProvider, client) => client.BaseAddress = HttpBaseAddress.From(Options(serviceProvider).Paymob.BaseUrl)).AddTimeoutResilience(serviceProvider => TimeSpan.FromSeconds(Options(serviceProvider).AttemptTimeoutSeconds), serviceProvider => TimeSpan.FromSeconds(Options(serviceProvider).TotalTimeoutSeconds), retryUnsafeMethods: false);
        services.AddScoped<FakePaymentGateway>();
        services.AddSingleton<IPaymentNotificationReader, PaymobNotificationReader>();
        services.AddProviderSwitch<IPaymentGateway, FakePaymentGateway, PaymobPaymentGateway>(UsesPaymob);
        return services;
    }

    private static bool UsesPaymob(IServiceProvider serviceProvider) => Options(serviceProvider).Provider switch
    {
        PaymentProvider.Fake => false,
        PaymentProvider.Paymob => true,
        _ => throw new InvalidOperationException("Unsupported Payments:Provider."),
    };

    private static void ApplyAllowFakePaymentsDefault(PaymentsOptions options, IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        if (string.IsNullOrWhiteSpace(configuration[PaymentsOptions.AllowFakePaymentsKey]))
        {
            options.AllowFakePayments = hostEnvironment.IsDevelopment();
        }
    }

    private static PaymentsOptions Options(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<PaymentsOptions>>().Value;
}
