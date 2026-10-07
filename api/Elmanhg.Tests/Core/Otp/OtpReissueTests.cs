using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Otp;

public sealed class OtpReissueTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reissue_BelowMax_IssuesNewCode()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(0).WithMaxReissueCount(2).IssuedAt(Now).Build();
        var firstVerificationId = otp.VerificationId;

        otp.Reissue("next-hash", 5, Now.AddMinutes(1));

        otp.CodeHash.Should().Be("next-hash");
        otp.ReissueCount.Should().Be(1);
        otp.ExpiresAt.Should().Be(Now.AddMinutes(6));
        otp.VerificationId.Should().NotBe(firstVerificationId);
        otp.IsVerified.Should().BeFalse();
    }

    [Fact]
    public void Reissue_BelowMax_ReturnsThePreviousState()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(0).WithMaxReissueCount(2).IssuedAt(Now).Build();
        var verificationId = otp.VerificationId;

        var previous = otp.Reissue("next-hash", 5, Now.AddMinutes(1));

        previous.VerificationId.Should().Be(verificationId);
        previous.CodeHash.Should().Be(OtpBuilder.CodeHash);
        previous.ReissueCount.Should().Be(0);
        previous.NextAllowedReissueAt.Should().Be(Now);
        previous.ExpiresAt.Should().Be(Now.AddMinutes(5));
        previous.CreatedAt.Should().Be(Now);
        previous.ReissueWindowStartedAt.Should().Be(Now);
    }

    [Fact]
    public void Reissue_DayAfterWindowStartDespiteRecentReissue_ResetsCount()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(0).WithMaxReissueCount(1).WithReissueBlockCooldownInHours(0).IssuedAt(Now).Build();
        otp.Reissue("first-hash", 5, Now.AddHours(23));

        otp.Reissue("second-hash", 5, Now.AddDays(1));

        otp.ReissueCount.Should().Be(1);
        otp.ReissueWindowStartedAt.Should().Be(Now.AddDays(1));
        otp.CodeHash.Should().Be("second-hash");
    }

    [Fact]
    public void Reissue_WithinWindow_KeepsWindowStart()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(0).WithMaxReissueCount(3).IssuedAt(Now).Build();

        otp.Reissue("next-hash", 5, Now.AddHours(5));

        otp.ReissueWindowStartedAt.Should().Be(Now);
        otp.CreatedAt.Should().Be(Now.AddHours(5));
    }

    [Fact]
    public void Reissue_BelowMax_NextAllowedIsCooldownFromResend()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(60).WithMaxReissueCount(3).IssuedAt(Now).Build();

        otp.Reissue("next-hash", 5, Now.AddMinutes(10));

        otp.NextAllowedReissueAt.Should().Be(Now.AddMinutes(10).AddSeconds(60));
    }
}
