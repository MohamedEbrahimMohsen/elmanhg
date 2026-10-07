using Core.DDD.Time;
using Core.OTP.Delivery;
using Core.OTP.Entities;
using Core.OTP.OtpHasher;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class OtpConcurrencyEndpointTests(ApiFactory factory)
{
    private const string SendRoute = "/api/auth/otp/send";
    private const string VerifyRoute = "/api/auth/otp/verify";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendOtp_ParallelFirstSends_DeliversOneCodeAndStoresOneRow()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();

        var responses = await SendInParallelAsync(client, phone, 8);

        responses.Count(x => x.Status == HttpStatusCode.OK).Should().Be(1);
        responses.Where(x => x.Status != HttpStatusCode.OK).Should().HaveCount(7).And.AllSatisfy(x => x.Should().Be((HttpStatusCode.TooManyRequests, "OTP_REISSUE_COOLDOWN")));
        factory.Otp.SendCountFor(phone).Should().Be(1);
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Otps.AsNoTracking().CountAsync(x => x.Recipient == phone, CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task SendOtp_ParallelResendsPastCooldown_DeliversOneCodeAndCountsOneResend()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        await SeedAsync(new OtpBuilder().ForPhone(phone).IssuedAt(DateTimeOffset.UtcNow.AddMinutes(-2)).Build());

        var responses = await SendInParallelAsync(client, phone, 8);

        responses.Count(x => x.Status == HttpStatusCode.OK).Should().Be(1);
        responses.Where(x => x.Status != HttpStatusCode.OK).Should().AllSatisfy(x => x.Should().BeOneOf((HttpStatusCode.Conflict, "OTP_MODIFIED_CONCURRENTLY"), (HttpStatusCode.TooManyRequests, "OTP_REISSUE_COOLDOWN")));
        factory.Otp.SendCountFor(phone).Should().Be(1);
        (await ReadOtpAsync(phone)).ReissueCount.Should().Be(1);
    }

    [Fact]
    public async Task VerifyOtp_ParallelWrongCodes_CountsEveryAnsweredAttempt()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        var verificationId = await AuthTestClient.SendOtpAsync(client, phone, CancellationToken);
        var wrongCode = factory.Otp.LatestCodeFor(phone) == "000000" ? "111111" : "000000";

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => PostAsync(client, VerifyRoute, new { code = wrongCode, verificationId })));

        responses.Should().AllSatisfy(x => x.Should().BeOneOf((HttpStatusCode.BadRequest, "OTP_NOT_MATCHED"), (HttpStatusCode.TooManyRequests, "OTP_REACHED_MAX_ATTEMPTS"), (HttpStatusCode.Conflict, "OTP_MODIFIED_CONCURRENTLY")));
        responses.Count(x => x.Code == "OTP_NOT_MATCHED").Should().BeLessThanOrEqualTo(3);
        (await ReadOtpAsync(phone)).VerificationAttempts.Should().Be(responses.Count(x => x.Status != HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task SendOtp_ResendDeliveryFails_KeepsThePreviousCodeAndLimits()
    {
        var phone = AuthTestClient.NewPhoneNumber();
        var codeHash = factory.Services.GetRequiredService<IOtpHasher>().Hash("123456");
        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-2).TruncateToMicroseconds();
        var seeded = Otp.Create(OtpRecipientType.Phone, phone, codeHash, 5, 3, 60, 5, 24, issuedAt);
        await SeedAsync(seeded);
        await using var noPhoneChannel = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.PostConfigure<OtpDeliveryOptions>(options =>
        {
            options.WhatsApp.Enabled = false;
            options.Sms.Enabled = false;
        })));
        using var failingClient = noPhoneChannel.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var failed = await PostAsync(failingClient, SendRoute, new { phoneNumber = phone });

        failed.Should().Be((HttpStatusCode.ServiceUnavailable, "OTP_CHANNEL_UNAVAILABLE"));
        var stored = await ReadOtpAsync(phone);
        (stored.ReissueCount, stored.VerificationId, stored.NextAllowedReissueAt).Should().Be((0, seeded.VerificationId, seeded.NextAllowedReissueAt));
        using var client = AuthTestClient.Create(factory);
        using var verify = await client.PostAsJsonAsync(VerifyRoute, new { code = "123456", verificationId = seeded.VerificationId }, CancellationToken);
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static Task<(HttpStatusCode Status, string? Code)[]> SendInParallelAsync(HttpClient client, string phone, int count) => Task.WhenAll(Enumerable.Range(0, count).Select(_ => PostAsync(client, SendRoute, new { phoneNumber = phone })));

    private static async Task<(HttpStatusCode Status, string? Code)> PostAsync(HttpClient client, string route, object body)
    {
        using var response = await client.PostAsJsonAsync(route, body, CancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken).ConfigureAwait(false);
        return (response.StatusCode, json.TryGetProperty("code", out var code) ? code.GetString() : null);
    }

    private async Task SeedAsync(Otp otp)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Otps.Add(otp);
        await context.SaveChangesAsync(CancellationToken);
    }

    private async Task<Otp> ReadOtpAsync(string phone)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Otps.AsNoTracking().SingleAsync(x => x.Recipient == phone, CancellationToken);
    }
}
