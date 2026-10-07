using Core.Errors;
using Core.OTP.Entities;
using Core.OTP.Exceptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using OtpEntity = Core.OTP.Entities.Otp;

namespace Elmanhg.Tests.Core.Otp;

public sealed class OtpTests
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
    public void RestoreReissue_AfterReissue_RestoresThePreviousCodeAndLimits()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();
        otp.Verify("wrong-hash", Now);
        var before = Snapshot(otp);
        var previous = otp.Reissue("next-hash", 5, Now.AddMinutes(2));

        otp.RestoreReissue(previous);

        Snapshot(otp).Should().Be(before);
        otp.Verify(OtpBuilder.CodeHash, Now.AddMinutes(2)).Should().BeNull();
    }

    [Fact]
    public void RestoreReissue_AfterWindowReset_RestoresTheOldWindowAndCount()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(0).WithMaxReissueCount(5).IssuedAt(Now.AddDays(-1)).Build();
        otp.Reissue("first-hash", 5, Now.AddDays(-1).AddMinutes(1));
        otp.Reissue("second-hash", 5, Now.AddDays(-1).AddMinutes(2));
        var previous = otp.Reissue("third-hash", 5, Now);
        otp.ReissueWindowStartedAt.Should().Be(Now);

        otp.RestoreReissue(previous);

        otp.ReissueCount.Should().Be(2);
        otp.ReissueWindowStartedAt.Should().Be(Now.AddDays(-1));
        otp.CodeHash.Should().Be("second-hash");
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

    [Fact]
    public void Reissue_BelowMax_NextAllowedIsCooldownFromResend()
    {
        var otp = new OtpBuilder().WithReissueCooldownSeconds(60).WithMaxReissueCount(3).IssuedAt(Now).Build();

        otp.Reissue("next-hash", 5, Now.AddMinutes(10));

        otp.NextAllowedReissueAt.Should().Be(Now.AddMinutes(10).AddSeconds(60));
    }

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

    [Fact]
    public void MarkUsed_ExpiredAtGivenTime_ThrowsExpired()
    {
        var otp = new OtpBuilder().Verified().IssuedAt(Now).Build();

        var act = () => otp.MarkUsed(Now.AddMinutes(5));

        act.Should().Throw<BadRequestCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.OTPExpired);
        otp.IsUsed.Should().BeFalse();
    }

    private static OtpReissueState Snapshot(OtpEntity otp) => new(otp.VerificationId, otp.CodeHash, otp.IsVerified, otp.IsUsed, otp.VerificationAttempts, otp.ReissueCount, otp.ReissueWindowStartedAt, otp.CreatedAt, otp.ExpiresAt, otp.NextAllowedReissueAt);
}
