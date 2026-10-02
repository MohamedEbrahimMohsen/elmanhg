using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Infrastructure.Invitations;
using Elmanhg.Infrastructure.Messaging;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Infrastructure.OtpDelivery.Email;
using Elmanhg.Infrastructure.OtpDelivery.Sms;
using Elmanhg.Infrastructure.OtpDelivery.WhatsApp;
using Elmanhg.Infrastructure.Payments;
using Elmanhg.Infrastructure.Storage;

namespace Elmanhg.Infrastructure.Hosting;

public static class InfrastructureIntegrations
{
    public static List<IntegrationProviderResult> Describe(OtpDeliveryOptions otpDelivery, OutOfAppReminderOptions outOfAppReminders, PaymentsOptions payments, FileStorageOptions fileStorage, AiServiceOptions aiService)
    {
        var usesResend = InvitationEmailServiceCollectionExtensions.UsesResend(otpDelivery.Email);
        var fileStorageProvider = fileStorage.Provider ?? FileStorageProvider.Local;
        return
        [
            Channel("otpWhatsApp", otpDelivery.WhatsApp.Enabled, otpDelivery.WhatsApp.Provider.ToString(), otpDelivery.WhatsApp.Provider == WhatsAppProvider.Fake),
            Channel("otpEmail", otpDelivery.Email.Enabled, otpDelivery.Email.Provider.ToString(), otpDelivery.Email.Provider == EmailProvider.Fake),
            Channel("otpSms", otpDelivery.Sms.Enabled, otpDelivery.Sms.Provider.ToString(), otpDelivery.Sms.Provider == SmsProvider.Fake),
            new("invitationEmail", usesResend ? nameof(EmailProvider.Resend) : nameof(EmailProvider.Fake), usesResend ? IntegrationMode.Real : IntegrationMode.Fake, true),
            new("teacherReminderWhatsApp", otpDelivery.WhatsApp.Provider.ToString(), MessagingServiceCollectionExtensions.UsesMeta(otpDelivery.WhatsApp, outOfAppReminders) ? IntegrationMode.Real : IntegrationMode.Fake, otpDelivery.WhatsApp.Enabled),
            new("teacherReminderEmail", usesResend ? nameof(EmailProvider.Resend) : nameof(EmailProvider.Fake), usesResend ? IntegrationMode.Real : IntegrationMode.Fake, true),
            new("payments", payments.Provider.ToString(), payments.Provider == PaymentProvider.Fake ? IntegrationMode.Fake : IntegrationMode.Real, true),
            new("fileStorage", fileStorageProvider.ToString(), fileStorageProvider == FileStorageProvider.S3 ? IntegrationMode.Real : IntegrationMode.Local, true),
            new("aiService", aiService.Provider.ToString(), aiService.Provider == AiServiceProvider.Http ? IntegrationMode.Real : IntegrationMode.Fake, true),
        ];
    }

    private static IntegrationProviderResult Channel(string integration, bool enabled, string provider, bool isFakeProvider) => new(integration, provider, enabled && !isFakeProvider ? IntegrationMode.Real : IntegrationMode.Fake, enabled);
}
