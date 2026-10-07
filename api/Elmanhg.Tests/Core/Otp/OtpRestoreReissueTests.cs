using Core.OTP.Entities;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using OtpEntity = Core.OTP.Entities.Otp;

namespace Elmanhg.Tests.Core.Otp;

public sealed class OtpRestoreReissueTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

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

    private static OtpReissueState Snapshot(OtpEntity otp) => new(otp.VerificationId, otp.CodeHash, otp.IsVerified, otp.IsUsed, otp.VerificationAttempts, otp.ReissueCount, otp.ReissueWindowStartedAt, otp.CreatedAt, otp.ExpiresAt, otp.NextAllowedReissueAt);
}
