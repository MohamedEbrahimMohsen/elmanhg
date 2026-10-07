using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static Elmanhg.Tests.Integration.Auth.OtpEndpointTestData;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class OtpConcurrencyEndpointTests(ApiFactory factory)
{
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
        await SeedAsync(factory, new OtpBuilder().ForPhone(phone).IssuedAt(DateTimeOffset.UtcNow.AddMinutes(-2)).Build(), CancellationToken);

        var responses = await SendInParallelAsync(client, phone, 8);

        responses.Count(x => x.Status == HttpStatusCode.OK).Should().Be(1);
        responses.Where(x => x.Status != HttpStatusCode.OK).Should().AllSatisfy(x => x.Should().BeOneOf((HttpStatusCode.Conflict, "OTP_MODIFIED_CONCURRENTLY"), (HttpStatusCode.TooManyRequests, "OTP_REISSUE_COOLDOWN")));
        factory.Otp.SendCountFor(phone).Should().Be(1);
        (await ReadOtpAsync(factory, phone, CancellationToken)).ReissueCount.Should().Be(1);
    }

    [Fact]
    public async Task VerifyOtp_ParallelWrongCodes_CountsEveryAnsweredAttempt()
    {
        using var client = AuthTestClient.Create(factory);
        var phone = AuthTestClient.NewPhoneNumber();
        var verificationId = await AuthTestClient.SendOtpAsync(client, phone, CancellationToken);
        var wrongCode = factory.Otp.LatestCodeFor(phone) == "000000" ? "111111" : "000000";

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => PostAsync(client, VerifyRoute, new { code = wrongCode, verificationId }, CancellationToken)));

        responses.Should().AllSatisfy(x => x.Should().BeOneOf((HttpStatusCode.BadRequest, "OTP_NOT_MATCHED"), (HttpStatusCode.TooManyRequests, "OTP_REACHED_MAX_ATTEMPTS"), (HttpStatusCode.Conflict, "OTP_MODIFIED_CONCURRENTLY")));
        responses.Count(x => x.Code == "OTP_NOT_MATCHED").Should().BeLessThanOrEqualTo(3);
        (await ReadOtpAsync(factory, phone, CancellationToken)).VerificationAttempts.Should().Be(responses.Count(x => x.Status != HttpStatusCode.Conflict));
    }

    private static Task<(HttpStatusCode Status, string? Code)[]> SendInParallelAsync(HttpClient client, string phone, int count) => Task.WhenAll(Enumerable.Range(0, count).Select(_ => PostAsync(client, SendRoute, new { phoneNumber = phone }, CancellationToken)));
}
