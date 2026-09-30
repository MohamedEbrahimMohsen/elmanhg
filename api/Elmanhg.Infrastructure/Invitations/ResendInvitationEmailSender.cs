using Elmanhg.Application.Shared.Email;
using Elmanhg.Infrastructure.OtpDelivery;
using Elmanhg.Infrastructure.OtpDelivery.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Elmanhg.Infrastructure.Invitations;

public sealed class ResendInvitationEmailSender(HttpClient httpClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, IOptions<InvitationEmailOptions> invitationEmailOptions, ILogger<ResendInvitationEmailSender> logger) : IInvitationEmailSender
{
    private const string EmailsPath = "emails";
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string BearerScheme = "Bearer";

    public async Task<bool> SendAsync(InvitationEmail invitation, CancellationToken cancellationToken)
    {
        var email = otpDeliveryOptions.Value.Email;
        var options = invitationEmailOptions.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, EmailsPath)
        {
            Content = JsonContent.Create(new ResendEmailMessage(email.FromAddress, [invitation.Email], options.Subject, InvitationEmailTemplate.RenderHtml(invitation.DisplayName, invitation.Role, options.AcceptInviteUrl), InvitationEmailTemplate.RenderText(invitation.DisplayName, invitation.Role, options.AcceptInviteUrl))),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, email.ApiKey);
        request.Headers.Add(IdempotencyKeyHeader, Guid.NewGuid().ToString("N"));
        request.Headers.UserAgent.ParseAdd(OtpProviderHttpExtensions.UserAgent);
        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            logger.LogWarning("The {Role} invitation email was rejected by Resend with HTTP {StatusCode}; the admin shares the link instead.", invitation.Role, (int)response.StatusCode);
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or ExecutionRejectedException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(exception, "The {Role} invitation email failed before Resend answered; the admin shares the link instead.", invitation.Role);
            return false;
        }
    }
}
