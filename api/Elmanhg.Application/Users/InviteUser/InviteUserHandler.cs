using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Email;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Application.Users.InviteUser;

public sealed class InviteUserHandler(UserManager<User> userManager, IInvitationEmailSender invitationEmailSender, ICurrentUserService currentUserService) : IRequestHandler<InviteUserCommand, InviteUserResult>
{
    public async Task<InviteUserResult> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var email = request.Email.Trim();
        var displayName = request.DisplayName.Trim();
        if (await userManager.FindByEmailAsync(email).ConfigureAwait(false) is not null)
        {
            throw new ConflictCoreException(ErrorCodes.EmailAlreadyRegistered);
        }

        var user = request.Role == UserRole.Admin ? User.CreateAdmin(displayName, email) : User.CreateTeacher(displayName, email);
        var result = await userManager.CreateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new BadRequestCoreException(ErrorCodes.UserCreationFailed, innerException: new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code))));
        }

        var emailSent = await invitationEmailSender.SendAsync(new InvitationEmail(email, displayName, user.Role), cancellationToken).ConfigureAwait(false);
        return new InviteUserResult(user.Id, emailSent);
    }
}
