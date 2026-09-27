using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Core.OTP.Repositories;
using Elmanhg.Application.Auth.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Application.Auth.LoginWithPhone;

public sealed class LoginWithPhoneHandler(UserManager<User> userManager, IOtpRepository otpRepository, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService) : IRequestHandler<LoginWithPhoneCommand, AuthResult>
{
    public async Task<AuthResult> Handle(LoginWithPhoneCommand request, CancellationToken cancellationToken)
    {
        var otp = await otpRepository.FindByVerificationId(request.VerificationId, cancellationToken).ConfigureAwait(false);
        if (otp is null)
        {
            throw new BadRequestCoreException(ErrorCodes.OtpInvalid);
        }

        otp.MarkUsed();

        var user = await userManager.FindByNameAsync(otp.PhoneNumber).ConfigureAwait(false);
        if (user is null)
        {
            throw new NotFoundCoreException(ErrorCodes.PhoneNumberNotRegistered);
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
