using Elmanhg.Domain.Identity;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class PhoneAuthEndpointTests(ApiFactory factory)
{
    private const string RegisterRoute = "/api/auth/register/phone";
    private const string LoginRoute = "/api/auth/login/phone";

    [Fact]
    public async Task RegisterWithPhone_VerifiedOtp_Returns200AndSetsRefreshCookie()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        var verificationId = await AuthTestClient.SendAndVerifyOtpAsync(client, factory, phone, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(RegisterRoute, new { verificationId, displayName = "Ahmed" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("user").GetProperty("role").GetString().Should().Be("Student");
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("elmanhg_refresh=", StringComparison.Ordinal)).ToLowerInvariant();
        cookie.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict").And.Contain("path=/api/auth");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.Users.AsNoTracking().SingleAsync(x => x.PhoneNumber == phone, TestContext.Current.CancellationToken);
        user.PhoneNumberConfirmed.Should().BeTrue();
        user.DisplayName.Should().Be("Ahmed");
        (await context.Otps.AsNoTracking().SingleAsync(x => x.VerificationId == verificationId, TestContext.Current.CancellationToken)).IsUsed.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterWithPhone_UnverifiedOtp_Returns400()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        var verificationId = await AuthTestClient.SendOtpAsync(client, phone, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(RegisterRoute, new { verificationId, displayName = "Ahmed" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("OTP_NOT_VERIFIED");
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(x => x.PhoneNumber == phone, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterWithPhone_EmailVerificationId_Returns400()
    {
        using var client = AuthTestClient.Create(factory);
        var email = AuthTestClient.NewEmail();
        var verificationId = await AuthTestClient.SendAndVerifyEmailOtpAsync(client, factory, email, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(RegisterRoute, new { verificationId, displayName = "Ahmed" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("OTP_INVALID");
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(x => x.UserName == email || x.PhoneNumber == email, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterWithPhone_PhoneAlreadyRegistered_Returns409()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        await AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithPhone("Existing", phone), null, false, TestContext.Current.CancellationToken);
        var verificationId = await AuthTestClient.SendAndVerifyOtpAsync(client, factory, phone, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(RegisterRoute, new { verificationId, displayName = "Ahmed" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("PHONE_NUMBER_ALREADY_REGISTERED");
    }

    [Fact]
    public async Task LoginWithPhone_RegisteredPhone_Returns200WithStudentRoleClaim()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        await AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithPhone("Ahmed", phone), null, false, TestContext.Current.CancellationToken);
        var verificationId = await AuthTestClient.SendAndVerifyOtpAsync(client, factory, phone, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(LoginRoute, new { verificationId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(body.GetProperty("accessToken").GetString());
        token.Claims.Should().ContainSingle(x => x.Type == ClaimTypes.Role).Which.Value.Should().Be("Student");
    }

    [Fact]
    public async Task LoginWithPhone_UnregisteredPhone_Returns404AndOtpStaysUsable()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        var verificationId = await AuthTestClient.SendAndVerifyOtpAsync(client, factory, phone, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(LoginRoute, new { verificationId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("PHONE_NUMBER_NOT_REGISTERED");
        using var register = await client.PostAsJsonAsync(RegisterRoute, new { verificationId, displayName = "Ahmed" }, TestContext.Current.CancellationToken);
        register.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginWithPhone_SuspendedUser_Returns403()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        await AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithPhone("Ahmed", phone), null, true, TestContext.Current.CancellationToken);
        var verificationId = await AuthTestClient.SendAndVerifyOtpAsync(client, factory, phone, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(LoginRoute, new { verificationId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadCodeAsync(response)).Should().Be("USER_SUSPENDED");
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
