using Core.OTP.Delivery;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Infrastructure.OtpDelivery.Email;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Infrastructure.OtpDelivery;

public static class OtpDeliveryServiceCollectionExtensions
{
    public static IServiceCollection AddOtpDelivery(this IServiceCollection services)
    {
        services.AddCoreOtpDelivery(new OtpDeliverySetup(ErrorCodes.OtpDeliveryFailed, ErrorCodes.OtpChannelUnavailable, OtpEmailTemplate.Render));
        services.AddSingleton<IOtpDeliveryObserver>(serviceProvider => serviceProvider.GetRequiredService<ElmanhgMetrics>());
        return services;
    }
}
