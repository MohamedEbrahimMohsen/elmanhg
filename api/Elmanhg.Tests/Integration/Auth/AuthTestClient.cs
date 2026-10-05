using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Auth;

public static class AuthTestClient
{
    public const string RefreshCookieName = "elmanhg_refresh";

    public static HttpClient Create(ApiFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    public static string NewPhoneNumber()
    {
        return "010" + (Math.Abs(Guid.NewGuid().GetHashCode()) % 100_000_000).ToString("D8", CultureInfo.InvariantCulture);
    }

    public static string NewEmail()
    {
        return $"{Guid.NewGuid():N}@elmanhg.test";
    }

    public static async Task<Guid> SendOtpAsync(HttpClient client, string phone, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/otp/send", new { phoneNumber = phone }, cancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
        return body.GetProperty("verificationId").GetGuid();
    }

    public static async Task<Guid> SendAndVerifyOtpAsync(HttpClient client, ApiFactory factory, string phone, CancellationToken cancellationToken)
    {
        var verificationId = await SendOtpAsync(client, phone, cancellationToken).ConfigureAwait(false);
        using var response = await client.PostAsJsonAsync("/api/auth/otp/verify", new { code = factory.Otp.LatestCodeFor(phone), verificationId }, cancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return verificationId;
    }

    public static async Task<Guid> SendEmailOtpAsync(HttpClient client, string email, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/otp/send", new { email }, cancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
        return body.GetProperty("verificationId").GetGuid();
    }

    public static async Task<Guid> SendAndVerifyEmailOtpAsync(HttpClient client, ApiFactory factory, string email, CancellationToken cancellationToken)
    {
        var verificationId = await SendEmailOtpAsync(client, email, cancellationToken).ConfigureAwait(false);
        using var response = await client.PostAsJsonAsync("/api/auth/otp/verify", new { code = factory.Otp.LatestCodeFor(email.ToLowerInvariant()), verificationId }, cancellationToken).ConfigureAwait(false);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return verificationId;
    }

    public static async Task<HttpResponseMessage> RegisterByPhoneAsync(HttpClient client, ApiFactory factory, string phone, CancellationToken cancellationToken)
    {
        var verificationId = await SendAndVerifyOtpAsync(client, factory, phone, cancellationToken).ConfigureAwait(false);
        return await client.PostAsJsonAsync("/api/auth/register/phone", new { verificationId, displayName = "Student", termsVersion = TermsVersions.Current }, cancellationToken).ConfigureAwait(false);
    }

    public static string ReadRefreshCookie(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal));
        return header.Split(';')[0];
    }

    public static async Task<User> SeedUserAsync(ApiFactory factory, User user, string? password, bool suspended, CancellationToken cancellationToken)
    {
        var scope = factory.Services.CreateAsyncScope();
        await using var scopeDisposal = scope.ConfigureAwait(false);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var result = password is null ? await userManager.CreateAsync(user).ConfigureAwait(false) : await userManager.CreateAsync(user, password).ConfigureAwait(false);
        result.Succeeded.Should().BeTrue();
        if (suspended)
        {
            user.Suspend();
            (await userManager.UpdateAsync(user).ConfigureAwait(false)).Succeeded.Should().BeTrue();
        }

        return user;
    }
}
