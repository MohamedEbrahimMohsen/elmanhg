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

namespace Elmanhg.Application.Auth.RegisterWithPhone;

public sealed class RegisterWithPhoneHandler(UserManager<User> userManager, IOtpRepository otpRepository, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService) : IRequestHandler<RegisterWithPhoneCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RegisterWithPhoneCommand request, CancellationToken cancellationToken)
    {
        var otp = await otpRepository.FindByVerificationId(request.VerificationId, cancellationToken).ConfigureAwait(false);
        if (otp is null || otp.RecipientType != OtpRecipientType.Phone)
        {
            throw new BadRequestCoreException(ErrorCodes.OtpInvalid);
        }

        otp.MarkUsed();

        if (await userManager.FindByNameAsync(otp.Recipient).ConfigureAwait(false) is not null)
        {
            throw new ConflictCoreException(ErrorCodes.PhoneNumberAlreadyRegistered);
        }

        var user = User.CreateStudentWithPhone(request.DisplayName, otp.Recipient);
        var result = await userManager.CreateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new BadRequestCoreException(ErrorCodes.UserCreationFailed, innerException: new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code))));
        }

        var accessToken = tokenService.GenerateTokenAsync(user.GetUserClaims());
        var refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken).ConfigureAwait(false);
        return AuthResultGenerator.Generate(user, accessToken, refreshToken);
    }
}
