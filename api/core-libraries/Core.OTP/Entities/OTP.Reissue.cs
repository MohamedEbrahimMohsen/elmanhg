namespace Core.OTP.Entities;

public partial class Otp
{
    public void RestoreReissue(OtpReissueState previous)
    {
        VerificationId = previous.VerificationId;
        CodeHash = previous.CodeHash;
        IsVerified = previous.IsVerified;
        IsUsed = previous.IsUsed;
        VerificationAttempts = previous.VerificationAttempts;
        ReissueCount = previous.ReissueCount;
        ReissueWindowStartedAt = previous.ReissueWindowStartedAt;
        CreatedAt = previous.CreatedAt;
        ExpiresAt = previous.ExpiresAt;
        NextAllowedReissueAt = previous.NextAllowedReissueAt;
    }

    private OtpReissueState CaptureReissueState() => new(VerificationId, CodeHash, IsVerified, IsUsed, VerificationAttempts, ReissueCount, ReissueWindowStartedAt, CreatedAt, ExpiresAt, NextAllowedReissueAt);
}
