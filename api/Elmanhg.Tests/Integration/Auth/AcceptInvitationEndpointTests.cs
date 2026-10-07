using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Users;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class AcceptInvitationEndpointTests(ApiFactory factory)
{
    private const string Route = "/api/auth/invitations/accept";
    private const string NewPassword = "Invited2026";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Accept_InvitedTeacher_SetsPasswordAndSignsIn()
    {
        var email = await InviteTeacherAsync();
        using var client = AuthTestClient.Create(factory);
        var verificationId = await AuthTestClient.SendAndVerifyEmailOtpAsync(client, factory, email, CancellationToken);

        using var response = await client.PostAsJsonAsync(Route, new { verificationId, password = NewPassword }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await UsersTestData.ReadBodyAsync(response, CancellationToken);
        body.GetProperty("user").GetProperty("role").GetString().Should().Be("Teacher");
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        AuthTestClient.ReadRefreshCookie(response).Should().StartWith($"{AuthTestClient.RefreshCookieName}=");
        using var login = await client.PostAsJsonAsync("/api/auth/login/email", new { email, password = NewPassword }, CancellationToken);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Accept_AfterAcceptance_Returns404()
    {
        var email = await InviteTeacherAsync();
        using var client = AuthTestClient.Create(factory);
        var first = await AuthTestClient.SendAndVerifyEmailOtpAsync(client, factory, email, CancellationToken);
        using (var accepted = await client.PostAsJsonAsync(Route, new { verificationId = first, password = NewPassword }, CancellationToken))
        {
            accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var second = await SeedVerifiedEmailOtpAsync(email);

        using var response = await client.PostAsJsonAsync(Route, new { verificationId = second, password = "Another2026" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UsersTestData.ReadCodeAsync(response, CancellationToken)).Should().Be("INVITATION_NOT_FOUND");
    }

    [Fact]
    public async Task Accept_StudentEmail_Returns404()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = AuthTestClient.Create(factory);
        var verificationId = await AuthTestClient.SendAndVerifyEmailOtpAsync(client, factory, student.Email!, CancellationToken);

        using var response = await client.PostAsJsonAsync(Route, new { verificationId, password = NewPassword }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await UsersTestData.ReadCodeAsync(response, CancellationToken)).Should().Be("INVITATION_NOT_FOUND");
    }

    [Fact]
    public async Task Accept_UnverifiedOtp_Returns400()
    {
        var email = await InviteTeacherAsync();
        using var client = AuthTestClient.Create(factory);
        var verificationId = await AuthTestClient.SendEmailOtpAsync(client, email, CancellationToken);

        using var response = await client.PostAsJsonAsync(Route, new { verificationId, password = NewPassword }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await UsersTestData.ReadCodeAsync(response, CancellationToken)).Should().Be("OTP_NOT_VERIFIED");
    }

    [Fact]
    public async Task Accept_ShortPassword_Returns422()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsJsonAsync(Route, new { verificationId = Guid.NewGuid(), password = "Ab1" }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await UsersTestData.ReadCodeAsync(response, CancellationToken)).Should().Contain("PASSWORD_TOO_SHORT");
    }

    private async Task<Guid> SeedVerifiedEmailOtpAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otp = new OtpBuilder().ForEmail(email.ToLowerInvariant()).Verified().Build();
        await context.Otps.Where(x => x.Recipient == otp.Recipient).ExecuteDeleteAsync(CancellationToken).ConfigureAwait(false);
        context.Otps.Add(otp);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return otp.VerificationId;
    }

    private async Task<string> InviteTeacherAsync()
    {
        var email = AuthTestClient.NewEmail();
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken).ConfigureAwait(false);
        using var adminClient = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken).ConfigureAwait(false);
        await UsersTestData.InviteAsync(adminClient, "Teacher", email, CancellationToken).ConfigureAwait(false);
        return email;
    }
}
