using Core.OTP.Entities;
using Core.OTP.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Otp;

public sealed class OtpTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_EmailRecipient_KeepsRecipientAndType()
    {
        var otp = global::Core.OTP.Entities.Otp.Create(OtpRecipientType.Email, "mona@elmanhg.test", OtpBuilder.CodeHash, 5, 3, 60, 5, 24, Now);

        otp.Recipient.Should().Be("mona@elmanhg.test");
        otp.RecipientType.Should().Be(OtpRecipientType.Email);
    }

    [Fact]
    public void Create_GivenNow_StampsTimesFromNow()
    {
        var otp = global::Core.OTP.Entities.Otp.Create(OtpRecipientType.Phone, "01012345678", OtpBuilder.CodeHash, 5, 3, 60, 5, 24, Now);

        otp.CreatedAt.Should().Be(Now);
        otp.ReissueWindowStartedAt.Should().Be(Now);
        otp.ExpiresAt.Should().Be(Now.AddMinutes(5));
        otp.NextAllowedReissueAt.Should().Be(Now.AddSeconds(60));
    }

    [Fact]
    public void Verify_MatchingHash_MarksVerified()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();

        var error = otp.Verify(OtpBuilder.CodeHash, Now);

        error.Should().BeNull();
        otp.IsVerified.Should().BeTrue();
        otp.VerificationAttempts.Should().Be(0);
    }

    [Fact]
    public void Verify_DifferentHashSameLength_ReturnsNotMatched()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();

        var error = otp.Verify("code-hasx", Now);

        error.Should().Be(ErrorCodes.OTPNotMatched);
        otp.IsVerified.Should().BeFalse();
    }

    [Fact]
    public void Verify_DifferentLengthHash_ReturnsNotMatched()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();

        var error = otp.Verify("short", Now);

        error.Should().Be(ErrorCodes.OTPNotMatched);
        otp.IsVerified.Should().BeFalse();
    }

    [Fact]
    public void Verify_ExpiredAtGivenTime_ReturnsExpired()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();

        var error = otp.Verify(OtpBuilder.CodeHash, Now.AddMinutes(5));

        error.Should().Be(ErrorCodes.OTPExpired);
        otp.IsVerified.Should().BeFalse();
    }
}
