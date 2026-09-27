using Core.Errors;
using Core.OTP.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Otp;

public sealed class OtpTests
{
    [Fact]
    public void Reissue_BeforeCooldown_ThrowsRateLimitExceeded()
    {
        var otp = new OtpBuilder().Build();

        var act = () => otp.Reissue("new-hash", 5);

        var exception = act.Should().Throw<RateLimitExceededCoreException>().Which;
        exception.ErrorCode.Should().Be(ErrorCodes.OTPReissueCooldown);
        exception.Context.Should().ContainKey("minutes");
    }

    [Fact]
    public void Reissue_PastMaxReissueCount_ThrowsRateLimitExceeded()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(0).WithMaxReissueCount(0).Build();
        otp.Reissue("first-hash", 5);

        var act = () => otp.Reissue("second-hash", 5);

        act.Should().Throw<RateLimitExceededCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.OTPReachedMaxReissueCount);
    }

    [Fact]
    public void MarkUsed_NotVerified_ThrowsBadRequest()
    {
        var otp = new OtpBuilder().Build();

        var act = otp.MarkUsed;

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.OTPNotVerified);
    }

    [Fact]
    public void MarkUsed_AlreadyUsed_ThrowsBadRequest()
    {
        var otp = new OtpBuilder().Verified().Build();
        otp.MarkUsed();

        var act = otp.MarkUsed;

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.OTPAlreadyUsed);
    }

    [Fact]
    public void MarkUsed_Verified_SetsIsUsed()
    {
        var otp = new OtpBuilder().Verified().Build();

        otp.MarkUsed();

        otp.IsUsed.Should().BeTrue();
    }
}
