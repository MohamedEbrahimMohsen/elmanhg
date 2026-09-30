using Elmanhg.Application.Shared.Email;
using Microsoft.Extensions.Logging;

namespace Elmanhg.Infrastructure.Invitations;

public sealed class FakeInvitationEmailSender(ILogger<FakeInvitationEmailSender> logger) : IInvitationEmailSender
{
    public Task<bool> SendAsync(InvitationEmail invitation, CancellationToken cancellationToken)
    {
        logger.LogInformation("FakeInvitationEmailSender is active; the {Role} invitation email was not delivered.", invitation.Role);
        return Task.FromResult(false);
    }
}
