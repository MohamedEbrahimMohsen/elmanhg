namespace Elmanhg.Application.Shared.Email;

public interface IInvitationEmailSender
{
    Task<bool> SendAsync(InvitationEmail invitation, CancellationToken cancellationToken);
}
