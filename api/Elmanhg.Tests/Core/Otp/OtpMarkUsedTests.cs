using Core.Errors;
using Core.OTP.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Otp;

public sealed class OtpMarkUsedTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MarkUsed_NotVerified_ThrowsBadRequest()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();

        var act = () => otp.MarkUsed(Now);

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.OTPNotVerified);
    }

    [Fact]
    public void MarkUsed_AlreadyUsed_ThrowsBadRequest()
    {
        var otp = new OtpBuilder().Verified().IssuedAt(Now).Build();
        otp.MarkUsed(Now);

        var act = () => otp.MarkUsed(Now);

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.OTPAlreadyUsed);
    }

    [Fact]
    public void MarkUsed_Verified_SetsIsUsed()
    {
        var otp = new OtpBuilder().Verified().IssuedAt(Now).Build();

        otp.MarkUsed(Now);

        otp.IsUsed.Should().BeTrue();
    }

    [Fact]
    public void MarkUsed_ExpiredAtGivenTime_ThrowsExpired()
    {
        var otp = new OtpBuilder().Verified().IssuedAt(Now).Build();

        var act = () => otp.MarkUsed(Now.AddMinutes(5));

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.OTPExpired);
        otp.IsUsed.Should().BeFalse();
    }
}
