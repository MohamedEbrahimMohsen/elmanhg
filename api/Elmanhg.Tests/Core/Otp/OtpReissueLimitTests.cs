using Core.Errors;
using Core.OTP.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Otp;

public sealed class OtpReissueLimitTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reissue_BeforeCooldown_ThrowsRateLimitExceeded()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();

        var act = () => otp.Reissue("new-hash", 5, Now);

        var exception = act.Should().Throw<RateLimitExceededCoreException>().Which;
        exception.ErrorCode.Should().Be(ErrorCodes.OTPReissueCooldown);
        exception.Context.Should().ContainKey("minutes");
    }

    [Fact]
    public void Reissue_PastMaxReissueCount_ThrowsRateLimitExceeded()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(0).WithMaxReissueCount(1).WithReissueBlockCooldownInHours(0).IssuedAt(Now).Build();
        otp.Reissue("first-hash", 5, Now);

        var act = () => otp.Reissue("second-hash", 5, Now.AddMinutes(1));

        act.Should().Throw<RateLimitExceededCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.OTPReachedMaxReissueCount);
    }

    [Fact]
    public void Reissue_MaxReissueCountZero_ThrowsOnFirstReissue()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(0).WithMaxReissueCount(0).IssuedAt(Now).Build();

        var act = () => otp.Reissue("first-hash", 5, Now.AddMinutes(1));

        act.Should().Throw<RateLimitExceededCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.OTPReachedMaxReissueCount);
    }

    [Fact]
    public void Reissue_LastAllowedResend_BlocksFromThatResend()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(60).WithMaxReissueCount(1).WithReissueBlockCooldownInHours(24).IssuedAt(Now).Build();

        otp.Reissue("next-hash", 5, Now.AddHours(2));

        otp.NextAllowedReissueAt.Should().Be(Now.AddHours(26));
    }

    [Fact]
    public void Reissue_BlockedAfterLastAllowedResend_ThrowsCooldownUntilBlockEnds()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(60).WithMaxReissueCount(1).WithReissueBlockCooldownInHours(24).IssuedAt(Now).Build();
        otp.Reissue("next-hash", 5, Now.AddHours(2));

        var act = () => otp.Reissue("blocked-hash", 5, Now.AddHours(25));

        var exception = act.Should().Throw<RateLimitExceededCoreException>().Which;
        exception.ErrorCode.Should().Be(ErrorCodes.OTPReissueCooldown);
        exception.Context.Should().ContainKey("hours").WhoseValue.Should().Be(1);
    }
}
