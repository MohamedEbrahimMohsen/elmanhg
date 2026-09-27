using Core.OTP.Sms;
using Elmanhg.Infrastructure.Sms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<SmsOptions>().BindConfiguration(SmsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddScoped<FakeSmsSender>();
        services.AddScoped<ISmsSender>(serviceProvider => serviceProvider.GetRequiredService<IOptions<SmsOptions>>().Value.Provider switch
        {
            SmsProvider.Fake => serviceProvider.GetRequiredService<FakeSmsSender>(),
            _ => throw new InvalidOperationException("Unsupported Sms:Provider."),
        });
        return services;
    }
}
