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

public sealed class EmailAuthEndpointTests(ApiFactory factory)
{
    private const string RegisterRoute = "/api/auth/register/email";
    private const string LoginRoute = "/api/auth/login/email";
    private const string Password = "Password1";

    [Fact]
    public async Task RegisterWithEmail_NewEmail_Returns200AndPersistsStudent()
    {
        using var client = AuthTestClient.Create(factory);
        var email = AuthTestClient.NewEmail();

        using var response = await client.PostAsJsonAsync(RegisterRoute, new { displayName = "Mona", email, password = Password }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthTestClient.ReadRefreshCookie(response).Should().StartWith("elmanhg_refresh=");
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AsNoTracking().SingleAsync(x => x.Email == email, TestContext.Current.CancellationToken);
        user.Role.Should().Be(UserRole.Student);
    }

    [Fact]
    public async Task RegisterWithEmail_DuplicateEmail_Returns409()
    {
        using var client = AuthTestClient.Create(factory);
        var email = AuthTestClient.NewEmail();
        using var first = await client.PostAsJsonAsync(RegisterRoute, new { displayName = "Mona", email, password = Password }, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(RegisterRoute, new { displayName = "Mona", email, password = Password }, TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("EMAIL_ALREADY_REGISTERED");
    }

    [Fact]
    public async Task RegisterWithEmail_ShortPassword_Returns422()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsJsonAsync(RegisterRoute, new { displayName = "Mona", email = AuthTestClient.NewEmail(), password = "Pass1" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("PASSWORD_TOO_SHORT");
    }

    [Fact]
    public async Task LoginWithEmail_Admin_Returns200WithAdminRoleClaim()
    {
        using var client = AuthTestClient.Create(factory);
        var email = AuthTestClient.NewEmail();
        await AuthTestClient.SeedUserAsync(factory, User.CreateAdmin("Admin", email), Password, false, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(LoginRoute, new { email, password = Password }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("user").GetProperty("role").GetString().Should().Be("Admin");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(body.GetProperty("accessToken").GetString());
        token.Claims.Should().ContainSingle(x => x.Type == ClaimTypes.Role).Which.Value.Should().Be("Admin");
    }

    [Fact]
    public async Task LoginWithEmail_WrongPassword_Returns400()
    {
        using var client = AuthTestClient.Create(factory);
        var email = AuthTestClient.NewEmail();
        await AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithEmail("Mona", email), Password, false, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(LoginRoute, new { email, password = "Wrong1234" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("USER_INVALID_LOGIN");
    }

    [Fact]
    public async Task LoginWithEmail_AfterMaxFailedAttempts_Returns429()
    {
        using var client = AuthTestClient.Create(factory);
        var email = AuthTestClient.NewEmail();
        await AuthTestClient.SeedUserAsync(factory, User.CreateStudentWithEmail("Mona", email), Password, false, TestContext.Current.CancellationToken);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failed = await client.PostAsJsonAsync(LoginRoute, new { email, password = "Wrong1234" }, TestContext.Current.CancellationToken);
            failed.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        using var response = await client.PostAsJsonAsync(LoginRoute, new { email, password = Password }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(response)).Should().Be("USER_LOCKED_OUT");
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
