using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Users.CheckUserActive;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Elmanhg.Api.Authorization;

public static class ActiveUserTokenValidation
{
    public static IServiceCollection AddActiveUserTokenValidation(this IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            options.Events.OnTokenValidated = OnTokenValidated;
        });
        return services;
    }

    // JWTs outlive a suspension; this refuses a suspended or deleted user's access token, and one minted before the user's
    // security stamp last changed (suspension, sign-out), on its next request.
    public static async Task OnTokenValidated(TokenValidatedContext context)
    {
        var claim = context.Principal?.FindFirst(CurrentUserService.Constants.UserIdClaimType)?.Value;
        var securityStampFingerprint = context.Principal?.FindFirst(SecurityStampClaim.ClaimType)?.Value;
        if (!Guid.TryParse(claim, out var userId) || string.IsNullOrEmpty(securityStampFingerprint))
        {
            context.Fail(ErrorCodes.UserNotAuthenticated);
            return;
        }

        var sender = context.HttpContext.RequestServices.GetRequiredService<ISender>();
        if (!await sender.Send(new CheckUserActiveQuery(userId, securityStampFingerprint), context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            context.Fail(ErrorCodes.UserSuspended);
        }
    }
}
