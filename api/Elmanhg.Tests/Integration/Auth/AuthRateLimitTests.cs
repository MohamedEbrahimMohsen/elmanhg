using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class AuthRateLimitTests(ApiFactory factory)
{
    [Fact]
    public async Task SendOtp_OverIpLimit_Returns429TooManyRequests()
    {
        await using var limitedFactory = CreateLimitedFactory("Auth:OtpRequestPermitLimit");
        using var client = limitedFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var first = await client.PostAsJsonAsync("/api/auth/otp/send", new { phoneNumber = AuthTestClient.NewPhoneNumber() }, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync("/api/auth/otp/send", new { phoneNumber = AuthTestClient.NewPhoneNumber() }, TestContext.Current.CancellationToken);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(response)).Should().Be("TOO_MANY_REQUESTS");
    }

    [Fact]
    public async Task LoginWithEmail_OverIpLimit_Returns429TooManyRequests()
    {
        await using var limitedFactory = CreateLimitedFactory("Auth:CredentialPermitLimit");
        using var client = limitedFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var body = new { email = AuthTestClient.NewEmail(), password = "Password1" };
        using var first = await client.PostAsJsonAsync("/api/auth/login/email", body, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync("/api/auth/login/email", body, TestContext.Current.CancellationToken);

        first.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(response)).Should().Be("TOO_MANY_REQUESTS");
    }

    private WebApplicationFactory<Program> CreateLimitedFactory(string permitLimitKey)
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { [permitLimitKey] = "1" })));
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
