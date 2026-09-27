using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Application.Auth.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Elmanhg.Application.Auth.RegisterWithEmail;

public sealed class RegisterWithEmailHandler(UserManager<User> userManager, ITokenService tokenService, IRefreshTokenService<User, Guid> refreshTokenService) : IRequestHandler<RegisterWithEmailCommand, AuthResult>
{
    public async Task<AuthResult> Handle(RegisterWithEmailCommand request, CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(request.Email).ConfigureAwait(false) is not null)
        {
            throw new ConflictCoreException(ErrorCodes.EmailAlreadyRegistered);
        }

        var user = User.CreateStudentWithEmail(request.DisplayName, request.Email);
        var result = await userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new BadRequestCoreException(ErrorCodes.UserCreationFailed, innerException: new InvalidOperationException(string.Join(", ", result.Errors.Select(x => x.Code))));
        }

        var accessToken = tokenService.GenerateTokenAsync(user.GetUserClaims());
        var refreshToken = await refreshTokenService.GenerateTokenAsync(user, cancellationToken).ConfigureAwait(false);
        return AuthResultGenerator.Generate(user, accessToken, refreshToken);
    }
}
