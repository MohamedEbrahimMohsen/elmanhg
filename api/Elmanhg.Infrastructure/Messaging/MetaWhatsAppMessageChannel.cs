using Core.Messaging;
using Core.Messaging.WhatsApp;
using Core.OTP.Delivery;
using Elmanhg.Application.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Messaging;

public sealed class MetaWhatsAppMessageChannel(MetaWhatsAppClient metaWhatsAppClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, IOptions<OutOfAppReminderOptions> outOfAppReminderOptions, ILogger<MetaWhatsAppMessageChannel> logger) : IMessageChannel
{
    public MessageChannel Channel => MessageChannel.WhatsApp;

    public async Task<bool> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        var reminder = message as TeacherThreadReminderMessage ?? throw new NotSupportedException($"{message.GetType().Name} has no WhatsApp template.");
        var otp = otpDeliveryOptions.Value;
        var whatsApp = otp.WhatsApp;
        var reminders = outOfAppReminderOptions.Value;
        var body = MetaWhatsAppTemplateMessage.Create(PhoneNumberFormatter.ToInternational(reminder.Address, otp.CountryCallingCode), reminders.WhatsAppTemplateName, reminders.WhatsAppLanguageCode, [reminder.SubjectName, reminder.LessonName, ReminderDeadlineFormatter.Format(reminder.SlaDueAt, reminders.TimeZone)], reminders.WhatsAppThreadButton ? reminder.ThreadId.ToString() : null);
        return await metaWhatsAppClient.SendTemplateAsync(body, new MetaWhatsAppSender(whatsApp.ApiVersion, whatsApp.PhoneNumberId, whatsApp.AccessToken), cancellationToken).ToDeliveredAsync(Channel, nameof(TeacherThreadReminderMessage), logger, cancellationToken).ConfigureAwait(false);
    }
}
