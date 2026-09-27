using Core.Identity.Tokens;
using Core.OTP.GenerateOTP;
using Core.OTP.VerifyOTP;
using Elmanhg.Api.RateLimiting;
using Elmanhg.Application.Auth.LoginWithEmail;
using Elmanhg.Application.Auth.LoginWithPhone;
using Elmanhg.Application.Auth.Logout;
using Elmanhg.Application.Auth.RefreshAccessToken;
using Elmanhg.Application.Auth.RegisterWithEmail;
using Elmanhg.Application.Auth.RegisterWithPhone;
using Elmanhg.Application.Auth.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Controllers.Auth;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController(IMediator mediator, IOptions<AuthOptions> authOptions, IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    [HttpPost("otp/send", Name = "SendOtp")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.OtpRequests)]
    [ProducesResponseType<GenerateOTPResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> SendOtp([FromBody] GenerateOTPCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("otp/verify", Name = "VerifyOtp")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType<VerifyOTPResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult> VerifyOtp([FromBody] VerifyOTPCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    [HttpPost("register/phone", Name = "RegisterWithPhone")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    public Task<ActionResult> RegisterWithPhone([FromBody] RegisterWithPhoneCommand command, CancellationToken cancellationToken) => SendWithRefreshCookie(command, cancellationToken);

    [HttpPost("register/email", Name = "RegisterWithEmail")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    public Task<ActionResult> RegisterWithEmail([FromBody] RegisterWithEmailCommand command, CancellationToken cancellationToken) => SendWithRefreshCookie(command, cancellationToken);

    [HttpPost("login/phone", Name = "LoginWithPhone")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    public Task<ActionResult> LoginWithPhone([FromBody] LoginWithPhoneCommand command, CancellationToken cancellationToken) => SendWithRefreshCookie(command, cancellationToken);

    [HttpPost("login/email", Name = "LoginWithEmail")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthRateLimitPolicies.Credentials)]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    public Task<ActionResult> LoginWithEmail([FromBody] LoginWithEmailCommand command, CancellationToken cancellationToken) => SendWithRefreshCookie(command, cancellationToken);

    [HttpPost("refresh", Name = "RefreshAccessToken")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResult>(StatusCodes.Status200OK)]
    public Task<ActionResult> RefreshAccessToken(CancellationToken cancellationToken) => SendWithRefreshCookie(new RefreshAccessTokenCommand(Request.Cookies[authOptions.Value.RefreshTokenCookieName] ?? string.Empty), cancellationToken);

    [HttpPost("logout", Name = "Logout")]
    [Authorize(Policy = DefaultCodes.AuthenticatedUser)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> Logout(CancellationToken cancellationToken)
    {
        await mediator.Send(new LogoutCommand(), cancellationToken);
        Response.DeleteRefreshToken(authOptions.Value);
        return Ok();
    }

    private async Task<ActionResult> SendWithRefreshCookie(IRequest<AuthResult> command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        Response.AppendRefreshToken(result.RefreshToken, authOptions.Value, jwtOptions.Value.RefreshTokenExpirationDays);
        return Ok(result);
    }
}
