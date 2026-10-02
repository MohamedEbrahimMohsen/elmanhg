using Elmanhg.Application.Shared.Messaging;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Infrastructure.OtpDelivery.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Elmanhg.Infrastructure.Messaging;

public sealed class ResendEmailMessageChannel(HttpClient httpClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, IOptions<OutOfAppReminderOptions> outOfAppReminderOptions, ILogger<ResendEmailMessageChannel> logger) : IMessageChannel
{
    private const string EmailsPath = "emails";
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string BearerScheme = "Bearer";

    public MessageChannel Channel => MessageChannel.Email;

    public async Task<bool> SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        var reminder = message as TeacherThreadReminderMessage ?? throw new NotSupportedException($"{message.GetType().Name} has no email template.");
        var email = otpDeliveryOptions.Value.Email;
        var reminders = outOfAppReminderOptions.Value;
        var deadline = ReminderDeadlineFormatter.Format(reminder.SlaDueAt, reminders.TimeZone);
        var link = $"{reminders.ThreadLinkBaseUrl.TrimEnd('/')}/{reminder.ThreadId}";
        var subject = reminders.UsesEnglishEmail ? reminders.EmailSubjectEnglish : reminders.EmailSubjectArabic;
        using var request = new HttpRequestMessage(HttpMethod.Post, EmailsPath)
        {
            Content = JsonContent.Create(new ResendEmailMessage(email.FromAddress, [reminder.Address], subject, TeacherReminderEmailTemplate.RenderHtml(reminders.EmailLanguage, reminder, deadline, link), TeacherReminderEmailTemplate.RenderText(reminders.EmailLanguage, reminder, deadline, link))),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, email.ApiKey);
        request.Headers.Add(IdempotencyKeyHeader, $"teacher-reminder-{reminder.ThreadId:N}-{reminder.RecipientUserId:N}");
        return await httpClient.SendMessageRequestAsync(request, Channel, nameof(TeacherThreadReminderMessage), logger, cancellationToken).ConfigureAwait(false);
    }
}
