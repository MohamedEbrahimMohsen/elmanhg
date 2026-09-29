using Elmanhg.Application.Shared.Payments;
using Elmanhg.Infrastructure.Payments.Paymob;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Payments;

public static class PaymentsServiceCollectionExtensions
{
    public static IServiceCollection AddPayments(this IServiceCollection services)
    {
        services.AddOptions<PaymentsOptions>().BindConfiguration(PaymentsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<PaymentsOptions>, PaymentsOptionsValidator>();
        services.AddHttpClient<PaymobPaymentGateway>((serviceProvider, client) => client.BaseAddress = new Uri(Options(serviceProvider).Paymob.BaseUrl.TrimEnd('/') + "/"))
            .AddStandardResilienceHandler()
            .Configure((resilience, serviceProvider) =>
            {
                var payments = Options(serviceProvider);
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(payments.AttemptTimeoutSeconds);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(payments.TotalTimeoutSeconds);
                resilience.Retry.DisableForUnsafeHttpMethods();
            });
        services.AddScoped<FakePaymentGateway>();
        services.AddScoped<IPaymentGateway>(serviceProvider => Options(serviceProvider).Provider switch
        {
            PaymentProvider.Fake => serviceProvider.GetRequiredService<FakePaymentGateway>(),
            PaymentProvider.Paymob => serviceProvider.GetRequiredService<PaymobPaymentGateway>(),
            _ => throw new InvalidOperationException("Unsupported Payments:Provider."),
        });
        return services;
    }

    private static PaymentsOptions Options(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<PaymentsOptions>>().Value;
}
