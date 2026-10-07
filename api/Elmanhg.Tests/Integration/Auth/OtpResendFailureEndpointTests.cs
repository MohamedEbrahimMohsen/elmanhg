using Core.DDD.Time;
using Core.OTP.Delivery;
using Core.OTP.Entities;
using Core.OTP.OtpHasher;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using static Elmanhg.Tests.Integration.Auth.OtpEndpointTestData;

namespace Elmanhg.Tests.Integration.Auth;

public sealed class OtpResendFailureEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendOtp_ResendDeliveryFails_KeepsThePreviousCodeAndLimits()
    {
        var phone = AuthTestClient.NewPhoneNumber();
        var codeHash = factory.Services.GetRequiredService<IOtpHasher>().Hash("123456");
        var issuedAt = DateTimeOffset.UtcNow.AddMinutes(-2).TruncateToMicroseconds();
        var seeded = Otp.Create(OtpRecipientType.Phone, phone, codeHash, 5, 3, 60, 5, 24, issuedAt);
        await SeedAsync(factory, seeded, CancellationToken);
        await using var noPhoneChannel = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.PostConfigure<OtpDeliveryOptions>(options =>
        {
            options.WhatsApp.Enabled = false;
            options.Sms.Enabled = false;
        })));
        using var failingClient = noPhoneChannel.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

        var failed = await PostAsync(failingClient, SendRoute, new { phoneNumber = phone }, CancellationToken);

        failed.Should().Be((HttpStatusCode.ServiceUnavailable, "OTP_CHANNEL_UNAVAILABLE"));
        var stored = await ReadOtpAsync(factory, phone, CancellationToken);
        (stored.ReissueCount, stored.VerificationId, stored.NextAllowedReissueAt).Should().Be((0, seeded.VerificationId, seeded.NextAllowedReissueAt));
        using var client = AuthTestClient.Create(factory);
        using var verify = await client.PostAsJsonAsync(VerifyRoute, new { code = "123456", verificationId = seeded.VerificationId }, CancellationToken);
        verify.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
