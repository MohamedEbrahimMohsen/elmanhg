using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Infrastructure.OtpDelivery.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Elmanhg.Infrastructure.Messaging;

public sealed class MetaWhatsAppMessageChannel(HttpClient httpClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, IOptions<OutOfAppReminderOptions> outOfAppReminderOptions, ILogger<MetaWhatsAppMessageChannel> logger) : IMessageChannel
{
    private const string BearerScheme = "Bearer";

    public MessageChannel Channel => MessageChannel.WhatsApp;

    public async Task<bool> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        var reminder = message as TeacherThreadReminderMessage ?? throw new NotSupportedException($"{message.GetType().Name} has no WhatsApp template.");
        var otp = otpDeliveryOptions.Value;
        var whatsApp = otp.WhatsApp;
        var reminders = outOfAppReminderOptions.Value;
        var body = MetaWhatsAppTemplateMessage.CreateUtility(PhoneNumberFormatter.ToInternational(reminder.Address, otp.CountryCallingCode), reminders.WhatsAppTemplateName, reminders.WhatsAppLanguageCode, [reminder.SubjectName, reminder.LessonName, ReminderDeadlineFormatter.Format(reminder.SlaDueAt, reminders.TimeZone)], reminders.WhatsAppThreadButton ? reminder.ThreadId.ToString() : null);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{whatsApp.ApiVersion}/{whatsApp.PhoneNumberId}/messages")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, whatsApp.AccessToken);
        return await httpClient.SendMessageRequestAsync(request, Channel, nameof(TeacherThreadReminderMessage), logger, cancellationToken).ConfigureAwait(false);
    }
}
