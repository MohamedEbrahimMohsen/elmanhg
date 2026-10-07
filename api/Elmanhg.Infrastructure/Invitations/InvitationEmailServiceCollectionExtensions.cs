using Core.Http;
using Core.Messaging.Email;
using Core.OTP.Delivery;
using Core.OTP.Delivery.Email;
using Core.Utilities;
using Elmanhg.Application.Shared.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Invitations;

public static class InvitationEmailServiceCollectionExtensions
{
    public static IServiceCollection AddInvitationEmail(this IServiceCollection services)
    {
        services.AddValidatedOptions<InvitationEmailOptions, InvitationEmailOptionsValidator>(InvitationEmailOptions.SectionName);
        services.AddScopedHttpConsumer<ResendInvitationEmailSender, ResendEmailClient>((serviceProvider, client) => client.BaseAddress = HttpBaseAddress.From(OtpDelivery(serviceProvider).Email.BaseUrl)).AddOtpProviderResilience(retryUnsafeMethods: true);
        services.AddScoped<FakeInvitationEmailSender>();
        services.AddProviderSwitch<IInvitationEmailSender, FakeInvitationEmailSender, ResendInvitationEmailSender>(serviceProvider => UsesResend(OtpDelivery(serviceProvider).Email));
        return services;
    }

    public static bool UsesResend(EmailOtpOptions email) => email is { Enabled: true, Provider: EmailProvider.Resend };

    private static OtpDeliveryOptions OtpDelivery(IServiceProvider serviceProvider) => serviceProvider.GetRequiredService<IOptions<OtpDeliveryOptions>>().Value;
}
