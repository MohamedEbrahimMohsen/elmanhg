using Core.Errors;
using Core.OTP.Exceptions;

namespace Core.OTP.Entities;

public partial class Otp
{
    // The resend quota is per day, counted from the window's first code; resends never move the window.
    private static readonly TimeSpan ReissueWindow = TimeSpan.FromDays(1);

    public OtpReissueState Reissue(string newCodeHash, int expiresInMinutes, DateTimeOffset now)
    {
        var previous = CaptureReissueState();
        if (ReissueWindowStartedAt + ReissueWindow <= now)
        {
            ReissueCount = 0;
            ReissueWindowStartedAt = now;
        }

        if (now < NextAllowedReissueAt)
        {
            var cooldown = NextAllowedReissueAt - now;
            throw new RateLimitExceededCoreException(ErrorCodes.OTPReissueCooldown, context: new Dictionary<string, object>
            {
                ["days"] = cooldown.Days,
                ["hours"] = cooldown.Hours,
                ["minutes"] = cooldown.Minutes,
                ["seconds"] = cooldown.Seconds
            });
        }

        if (ReissueCount >= MaxReissueCount)
        {
            throw new RateLimitExceededCoreException(ErrorCodes.OTPReachedMaxReissueCount);
        }

        VerificationId = Guid.NewGuid();
        CodeHash = newCodeHash;
        IsVerified = false;
        IsUsed = false;
        VerificationAttempts = 0;
        ReissueCount++;
        CreatedAt = now;
        ExpiresAt = now.AddMinutes(expiresInMinutes);
        NextAllowedReissueAt = ReissueCount == MaxReissueCount ? now.AddHours(ReissueBlockCooldownInHours) : now.AddSeconds(ReissueCooldownSeconds);
        return previous;
    }

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
