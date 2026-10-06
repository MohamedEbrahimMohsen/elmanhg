using Core.Http;
using Core.Messaging.Email;
using Core.OTP.Delivery;
using Elmanhg.Application.Shared.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Invitations;

public sealed class ResendInvitationEmailSender(ResendEmailClient resendEmailClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, IOptions<InvitationEmailOptions> invitationEmailOptions, ILogger<ResendInvitationEmailSender> logger) : IInvitationEmailSender
{
    public async Task<bool> SendAsync(InvitationEmail invitation, CancellationToken cancellationToken)
    {
        var email = otpDeliveryOptions.Value.Email;
        var options = invitationEmailOptions.Value;
        var message = new EmailMessage(email.FromAddress, invitation.Email, options.Subject, InvitationEmailTemplate.RenderHtml(invitation.DisplayName, invitation.Role, options.AcceptInviteUrl), InvitationEmailTemplate.RenderText(invitation.DisplayName, invitation.Role, options.AcceptInviteUrl));
        var result = await resendEmailClient.SendAsync(message, email.ApiKey, Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        return result.Failure switch
        {
            HttpCallFailure.None => true,
            HttpCallFailure.Rejected => Rejected(invitation, result),
            _ => Unreachable(invitation, result),
        };
    }

    private bool Rejected(InvitationEmail invitation, HttpSendResult result)
    {
        logger.LogWarning("The {Role} invitation email was rejected by Resend with HTTP {StatusCode}; the admin shares the link instead.", invitation.Role, result.StatusCode);
        return false;
    }

    private bool Unreachable(InvitationEmail invitation, HttpSendResult result)
    {
        logger.LogWarning(result.Exception, "The {Role} invitation email failed before Resend answered; the admin shares the link instead.", invitation.Role);
        return false;
    }
}
