using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class EmailCodeAuthEndpointTests(ApiFactory factory)
{
    private const string LoginRoute = "/api/auth/login/email-code";
    private const string Password = "Password1";

    [Fact]
    public async Task LoginWithEmailCode_RegisteredStudent_Returns200AndSetsRefreshCookie()
    {
        using var client = AuthTestClient.Create(factory);
        var email = AuthTestClient.NewEmail();
        using var registration = await client.PostAsJsonAsync("/api/auth/register/email", new { displayName = "Mona", email, password = Password }, TestContext.Current.CancellationToken);
        registration.StatusCode.Should().Be(HttpStatusCode.OK);
        var verificationId = await AuthTestClient.SendAndVerifyEmailOtpAsync(client, factory, email, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(LoginRoute, new { verificationId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthTestClient.ReadRefreshCookie(response).Should().StartWith($"{AuthTestClient.RefreshCookieName}=");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("user").GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task LoginWithEmailCode_UnregisteredEmail_Returns404()
    {
        using var client = AuthTestClient.Create(factory);
        var verificationId = await AuthTestClient.SendAndVerifyEmailOtpAsync(client, factory, AuthTestClient.NewEmail(), TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(LoginRoute, new { verificationId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("EMAIL_NOT_REGISTERED");
    }

    [Fact]
    public async Task LoginWithEmailCode_PhoneVerificationId_Returns400()
    {
        using var client = AuthTestClient.Create(factory);
        var verificationId = await AuthTestClient.SendAndVerifyOtpAsync(client, factory, AuthTestClient.NewPhoneNumber(), TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(LoginRoute, new { verificationId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("OTP_INVALID");
    }

    [Fact]
    public async Task LoginWithEmailCode_TeacherAccount_Returns403()
    {
        using var client = AuthTestClient.Create(factory);
        var email = AuthTestClient.NewEmail();
        await AuthTestClient.SeedUserAsync(factory, User.CreateTeacher("Teacher", email), Password, suspended: false, TestContext.Current.CancellationToken);
        var verificationId = await AuthTestClient.SendAndVerifyEmailOtpAsync(client, factory, email, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(LoginRoute, new { verificationId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadCodeAsync(response)).Should().Be("EMAIL_CODE_SIGN_IN_NOT_ALLOWED");
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
