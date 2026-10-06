using Core.OTP.Delivery;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Invitations;

public sealed class InvitationEmailOptionsValidator(IOptions<OtpDeliveryOptions> otpDeliveryOptions) : IValidateOptions<InvitationEmailOptions>
{
    public ValidateOptionsResult Validate(string? name, InvitationEmailOptions options)
    {
        if (!InvitationEmailServiceCollectionExtensions.UsesResend(otpDeliveryOptions.Value.Email))
        {
            return ValidateOptionsResult.Success;
        }

        return Uri.TryCreate(options.AcceptInviteUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("InvitationEmail:AcceptInviteUrl must be an absolute https URL when OtpDelivery:Email uses the Resend provider.");
    }
}
