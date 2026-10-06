using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Core.OTP.Entities;
using Core.OTP.Repositories;
using Elmanhg.Application.Auth.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Application.Auth.AcceptInvitation;

public sealed class AcceptInvitationHandler(UserManager<User> userManager, IOtpRepository otpRepository, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService) : IRequestHandler<AcceptInvitationCommand, AuthResult>
{
    public async Task<AuthResult> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var otp = await otpRepository.ConsumeAsync(request.VerificationId, OtpRecipientType.Email, ErrorCodes.OtpInvalid, cancellationToken).ConfigureAwait(false);

        var user = await userManager.FindByEmailAsync(otp.Recipient).ConfigureAwait(false);
        if (user is null || !user.IsInvitationPending)
        {
            throw new NotFoundCoreException(ErrorCodes.InvitationNotFound);
        }

        if (!user.IsActive)
        {
            throw new ForbiddenCoreException(ErrorCodes.UserSuspended);
        }

        var result = await userManager.AddPasswordAsync(user, request.Password).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new BadRequestCoreException(ErrorCodes.PasswordRejected, innerException: new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code))));
        }

        await otpRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var accessToken = tokenService.GenerateTokenAsync(user.GetUserClaims());
        var refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken).ConfigureAwait(false);
        return AuthResultGenerator.Generate(user, accessToken, refreshToken);
    }
}
