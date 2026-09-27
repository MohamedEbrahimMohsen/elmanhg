using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class OtpEndpointTests(ApiFactory factory)
{
    private const string SendRoute = "/api/auth/otp/send";
    private const string VerifyRoute = "/api/auth/otp/verify";

    [Fact]
    public async Task SendOtp_ValidPhone_Returns200AndDeliversCode()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();

        using var response = await client.PostAsJsonAsync(SendRoute, new { phoneNumber = phone }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("verificationId").GetGuid().Should().NotBeEmpty();
        body.TryGetProperty("code", out _).Should().BeFalse();
        factory.Sms.LatestCodeFor(phone).Should().MatchRegex("^[0-9]{6}$");
    }

    [Fact]
    public async Task SendOtp_UnknownPrefix_Returns422()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsJsonAsync(SendRoute, new { phoneNumber = "01312345678" }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("VALIDATION_PHONE_NUMBER_INVALID_CELLULAR_CODE");
    }

    [Fact]
    public async Task SendOtp_WithinCooldown_Returns429()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        await AuthTestClient.SendOtpAsync(client, phone, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(SendRoute, new { phoneNumber = phone }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await ReadCodeAsync(response)).Should().Be("OTP_REISSUE_COOLDOWN");
    }

    [Fact]
    public async Task VerifyOtp_CorrectCode_Returns200()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        var verificationId = await AuthTestClient.SendOtpAsync(client, phone, TestContext.Current.CancellationToken);

        using var response = await client.PostAsJsonAsync(VerifyRoute, new { code = factory.Sms.LatestCodeFor(phone), verificationId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var otp = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Otps.AsNoTracking().SingleAsync(x => x.VerificationId == verificationId, TestContext.Current.CancellationToken);
        otp.IsVerified.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_Returns400()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        var verificationId = await AuthTestClient.SendOtpAsync(client, phone, TestContext.Current.CancellationToken);
        var wrongCode = factory.Sms.LatestCodeFor(phone) == "000000" ? "111111" : "000000";

        using var response = await client.PostAsJsonAsync(VerifyRoute, new { code = wrongCode, verificationId }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("OTP_NOT_MATCHED");
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
