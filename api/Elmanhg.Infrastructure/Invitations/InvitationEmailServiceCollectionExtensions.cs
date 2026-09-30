using Elmanhg.Application.Shared.Email;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Infrastructure.OtpDelivery.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Invitations;

public static class InvitationEmailServiceCollectionExtensions
{
    public static IServiceCollection AddInvitationEmail(this IServiceCollection services)
    {
        services.AddOptions<InvitationEmailOptions>().BindConfiguration(InvitationEmailOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<InvitationEmailOptions>, InvitationEmailOptionsValidator>();
        services.AddHttpClient<ResendInvitationEmailSender>((serviceProvider, client) => client.BaseAddress = OtpDeliveryServiceCollectionExtensions.BaseAddress(OtpDeliveryServiceCollectionExtensions.Options(serviceProvider).Email.BaseUrl)).AddOtpResilience(retryUnsafeMethods: true);
        services.AddScoped<FakeInvitationEmailSender>();
        services.AddScoped<IInvitationEmailSender>(serviceProvider => UsesResend(OtpDeliveryServiceCollectionExtensions.Options(serviceProvider).Email) ? serviceProvider.GetRequiredService<ResendInvitationEmailSender>() : serviceProvider.GetRequiredService<FakeInvitationEmailSender>());
        return services;
    }

    public static bool UsesResend(EmailOtpOptions email) => email is { Enabled: true, Provider: EmailProvider.Resend };
}
