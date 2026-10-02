using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Application.Teachers.SetTeacherPhoneNumber;

public sealed class SetTeacherPhoneNumberHandler(UserManager<User> userManager, ICurrentUserService currentUserService) : IRequestHandler<SetTeacherPhoneNumberCommand>
{
    public async Task Handle(SetTeacherPhoneNumberCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        var teacher = await userManager.FindByIdAsync(request.TeacherId.ToString()).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.UserNotFound);
        teacher.SetContactPhoneNumber(request.PhoneNumber, currentUserService.UserId.Value);
        var result = await userManager.UpdateAsync(teacher).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ConflictCoreException(ErrorCodes.UserModifiedConcurrently);
        }
    }
}
