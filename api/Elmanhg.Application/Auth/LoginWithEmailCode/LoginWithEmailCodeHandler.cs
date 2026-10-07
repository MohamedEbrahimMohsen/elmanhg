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

namespace Elmanhg.Application.Auth.LoginWithEmailCode;

public sealed class LoginWithEmailCodeHandler(UserManager<User> userManager, IOtpRepository otpRepository, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService, TimeProvider timeProvider) : IRequestHandler<LoginWithEmailCodeCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginWithEmailCodeCommand request, CancellationToken cancellationToken)
    {
        var otp = await otpRepository.ConsumeAsync(request.VerificationId, OtpRecipientType.Email, ErrorCodes.OtpInvalid, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        var user = await userManager.FindByEmailAsync(otp.Recipient).ConfigureAwait(false);
        if (user is null)
        {
            throw new NotFoundCoreException(ErrorCodes.EmailNotRegistered);
        }

        if (user.Role != UserRole.Student)
        {
            throw new ForbiddenCoreException(ErrorCodes.EmailCodeSignInNotAllowed);
        }

        if (!user.IsActive)
        {
            throw new ForbiddenCoreException(ErrorCodes.UserSuspended);
        }

        await otpRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var accessToken = tokenService.GenerateTokenAsync(user.GetUserClaims());
        var refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken).ConfigureAwait(false);
        return AuthResultGenerator.Generate(user, accessToken, refreshToken);
    }
}
