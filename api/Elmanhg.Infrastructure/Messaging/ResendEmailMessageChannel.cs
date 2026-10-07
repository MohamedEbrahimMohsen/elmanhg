using Core.Http;
using Core.Messaging.Email;
using Core.OTP.Delivery;
using Elmanhg.Application.Shared.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Messaging;

public sealed class ResendEmailMessageChannel(ResendEmailClient resendEmailClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, IOptions<OutOfAppReminderOptions> outOfAppReminderOptions, ILogger<ResendEmailMessageChannel> logger) : IMessageChannel
{
    public MessageChannel Channel => MessageChannel.Email;

    public async Task<bool> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        var reminder = message as TeacherThreadReminderMessage ?? throw new NotSupportedException($"{message.GetType().Name} has no email template.");
        var email = otpDeliveryOptions.Value.Email;
        var reminders = outOfAppReminderOptions.Value;
        var deadline = ReminderDeadlineFormatter.Format(reminder.SlaDueAt, reminders.TimeZone);
        var link = HttpBaseAddress.Combine(reminders.ThreadLinkBaseUrl, reminder.ThreadId.ToString());
        var subject = reminders.UsesEnglishEmail ? reminders.EmailSubjectEnglish : reminders.EmailSubjectArabic;
        var emailMessage = new EmailMessage(email.FromAddress, reminder.Address, subject, TeacherReminderEmailTemplate.RenderHtml(reminders.EmailLanguage, reminder, deadline, link), TeacherReminderEmailTemplate.RenderText(reminders.EmailLanguage, reminder, deadline, link));
        return await resendEmailClient.SendAsync(emailMessage, email.ApiKey, $"teacher-reminder-{reminder.ThreadId:N}-{reminder.RecipientUserId:N}", cancellationToken).ToDeliveredAsync(Channel, nameof(TeacherThreadReminderMessage), logger, cancellationToken).ConfigureAwait(false);
    }
}
