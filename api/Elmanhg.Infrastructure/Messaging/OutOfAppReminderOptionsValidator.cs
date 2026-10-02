using Elmanhg.Infrastructure.Invitations;
using Elmanhg.Infrastructure.OtpDelivery;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Messaging;

public sealed class OutOfAppReminderOptionsValidator(IOptions<OtpDeliveryOptions> otpDeliveryOptions) : IValidateOptions<OutOfAppReminderOptions>
{
    public ValidateOptionsResult Validate(string? name, OutOfAppReminderOptions options)
    {
        List<string> failures = [];
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(options.TimeZone, out _))
        {
            failures.Add("OutOfAppReminders:TimeZone must be a known time zone id.");
        }

        var linkRequired = InvitationEmailServiceCollectionExtensions.UsesResend(otpDeliveryOptions.Value.Email) || !string.IsNullOrWhiteSpace(options.ThreadLinkBaseUrl);
        if (linkRequired && !(Uri.TryCreate(options.ThreadLinkBaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps))
        {
            failures.Add("OutOfAppReminders:ThreadLinkBaseUrl must be an absolute https URL when OtpDelivery:Email uses the Resend provider or when it is set.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
