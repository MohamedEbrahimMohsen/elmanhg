using Core.Errors;
using Core.Identity.Exceptions;
using Core.Identity.Tokens;
using Core.Identity.Tokens.RefreshToken;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;

namespace Elmanhg.Tests.Core.Identity;

public sealed class RefreshTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly SignInManager<User> _signInManager = SignInManagerSubstitute.Create();
    private readonly ISecureDataFormat<AuthenticationTicket> _protector = Substitute.For<ISecureDataFormat<AuthenticationTicket>>();
    private readonly IOptionsMonitor<BearerTokenOptions> _bearerOptions = Substitute.For<IOptionsMonitor<BearerTokenOptions>>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly RefreshTokenService<User, Guid> _service;

    public RefreshTokenServiceTests()
    {
        _bearerOptions.Get(IdentityConstants.BearerScheme).Returns(new BearerTokenOptions { RefreshTokenProtector = _protector });
        _timeProvider.GetUtcNow().Returns(Now);
        _service = new RefreshTokenService<User, Guid>(_signInManager, _bearerOptions, Options.Create(new JwtOptions { RefreshTokenExpirationDays = 7 }), _timeProvider);
    }

    [Fact]
    public async Task GenerateTokenAsync_User_ExpiresConfiguredDaysAfterProviderTime()
    {
        var user = User.CreateTeacher("Teacher", "teacher@example.com");
        _signInManager.CreateUserPrincipalAsync(user).Returns(new ClaimsPrincipal(new ClaimsIdentity("Test")));
        AuthenticationTicket? ticket = null;
        _protector.Protect(Arg.Do<AuthenticationTicket>(x => ticket = x)).Returns("refresh-token");

        await _service.GenerateTokenAsync(user, TestContext.Current.CancellationToken);

        ticket!.Properties.ExpiresUtc.Should().Be(Now.AddDays(7));
    }

    [Fact]
    public async Task ValidateTokenAsync_TicketExpiredAtProviderTime_ThrowsRefreshTokenIsExpired()
    {
        var properties = new AuthenticationProperties { ExpiresUtc = Now.AddSeconds(-1) };
        _protector.Unprotect("t").Returns(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("Test")), properties, IdentityConstants.BearerScheme));

        var act = () => _service.ValidateTokenAsync("t", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RefreshTokenIsExpired);
        await _signInManager.DidNotReceive().ValidateSecurityStampAsync(Arg.Any<ClaimsPrincipal>());
    }
}
