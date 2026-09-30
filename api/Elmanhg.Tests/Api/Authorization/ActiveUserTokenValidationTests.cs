using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Api.Authorization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Users.CheckUserActive;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using System.Security.Claims;

namespace Elmanhg.Tests.Api.Authorization;

public sealed class ActiveUserTokenValidationTests
{
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task OnTokenValidated_ActiveUser_LeavesPrincipal()
    {
        _sender.Send(new CheckUserActiveQuery(_userId), Arg.Any<CancellationToken>()).Returns(true);
        var context = Context(_userId.ToString());

        await ActiveUserTokenValidation.OnTokenValidated(context);

        context.Result.Should().BeNull();
        context.Principal.Should().NotBeNull();
    }

    [Fact]
    public async Task OnTokenValidated_InactiveUser_Fails()
    {
        _sender.Send(new CheckUserActiveQuery(_userId), Arg.Any<CancellationToken>()).Returns(false);
        var context = Context(_userId.ToString());

        await ActiveUserTokenValidation.OnTokenValidated(context);

        context.Result!.Failure!.Message.Should().Be(ErrorCodes.UserSuspended);
    }

    [Fact]
    public async Task OnTokenValidated_MissingUserIdClaim_Fails()
    {
        var context = Context(null);

        await ActiveUserTokenValidation.OnTokenValidated(context);

        context.Result!.Failure!.Message.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _sender.DidNotReceive().Send(Arg.Any<CheckUserActiveQuery>(), Arg.Any<CancellationToken>());
    }

    private TokenValidatedContext Context(string? userId)
    {
        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddSingleton(_sender).BuildServiceProvider() };
        List<Claim> claims = userId is null ? [] : [new Claim(CurrentUserService.Constants.UserIdClaimType, userId)];
        return new TokenValidatedContext(httpContext, new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)), new JwtBearerOptions())
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme)),
        };
    }
}
