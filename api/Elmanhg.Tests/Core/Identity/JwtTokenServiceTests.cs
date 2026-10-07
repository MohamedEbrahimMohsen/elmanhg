using Core.Errors;
using Core.Identity.Exceptions;
using Core.Identity.Tokens;
using Core.Identity.Tokens.AccessToken;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Elmanhg.Tests.Core.Identity;

public sealed class JwtTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly ISecureDataFormat<AuthenticationTicket> _protector = Substitute.For<ISecureDataFormat<AuthenticationTicket>>();
    private readonly IOptionsMonitor<BearerTokenOptions> _bearerOptions = Substitute.For<IOptionsMonitor<BearerTokenOptions>>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly JwtTokenService<User, Guid> _service;

    public JwtTokenServiceTests()
    {
        _bearerOptions.Get(IdentityConstants.BearerScheme).Returns(new BearerTokenOptions { RefreshTokenProtector = _protector });
        _timeProvider.GetUtcNow().Returns(Now);
        var jwtOptions = Options.Create(new JwtOptions { Issuer = "elmanhg-test", Audience = "elmanhg-test", Key = new string('k', 64), ExpirationHours = 2 });
        _service = new JwtTokenService<User, Guid>(SignInManagerSubstitute.Create(), jwtOptions, _bearerOptions, _timeProvider);
    }

    [Fact]
    public void GenerateTokenAsync_Claims_ExpiresConfiguredHoursAfterProviderTime()
    {
        var token = _service.GenerateTokenAsync([new Claim(ClaimTypes.Name, "teacher@example.com")]);

        new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo.Should().Be(Now.AddHours(2).UtcDateTime);
    }

    [Fact]
    public async Task GenerateTokenAsync_RefreshTicketExpiredAtProviderTime_ThrowsRefreshTokenIsExpired()
    {
        var properties = new AuthenticationProperties { ExpiresUtc = Now.AddSeconds(-1) };
        _protector.Unprotect("t").Returns(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("Test")), properties, IdentityConstants.BearerScheme));

        var act = () => _service.GenerateTokenAsync("t", []);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.RefreshTokenIsExpired);
    }
}
