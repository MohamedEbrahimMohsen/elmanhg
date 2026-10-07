using Core.DDD.Entities;
using Core.Errors;
using Core.OTP.Exceptions;
using System.Security.Cryptography;
using System.Text;

namespace Core.OTP.Entities;

public class Otp : Entity
{
    // The resend quota is per day, counted from the window's first code; resends never move the window.
    private static readonly TimeSpan ReissueWindow = TimeSpan.FromDays(1);

    public Guid VerificationId { get; private set; }
    public string Recipient { get; private set; }
    public OtpRecipientType RecipientType { get; private set; }
    public string CodeHash { get; private set; }
    
    public string? RequestIP { get; private set; }
    public string? UserAgent { get; private set; }

    public int VerificationAttempts { get; private set; }
    public int MaxVerificationAttempts { get; private set; }

    public int ReissueCount { get; private set; }
    public int MaxReissueCount { get; private set; }
    public int ReissueCooldownSeconds { get; private set; }
    public int ReissueBlockCooldownInHours { get; private set; }
    public DateTimeOffset NextAllowedReissueAt { get; private set; }
    public DateTimeOffset ReissueWindowStartedAt { get; private set; }

    public bool IsVerified { get; private set; }
    public bool IsUsed { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    private Otp(Guid id): base(id) { }

    public static Otp Create(OtpRecipientType recipientType, string recipient, string codeHash, int expiresInMinutes, int maxVerificationAttempts, int reissueCooldownSeconds, int maxReissueCount, int reissueBlockCooldownInHours, DateTimeOffset now)
    {
        var id = Guid.NewGuid();
        return new Otp(id)
        {
            VerificationId = Guid.NewGuid(),
            RecipientType = recipientType,
            Recipient = recipient,
            CodeHash = codeHash,
            CreatedAt = now,
            ReissueWindowStartedAt = now,
            ExpiresAt = now.AddMinutes(expiresInMinutes),
            NextAllowedReissueAt = now.AddSeconds(reissueCooldownSeconds),
            ReissueCooldownSeconds = reissueCooldownSeconds,
            VerificationAttempts = 0,
            MaxVerificationAttempts = maxVerificationAttempts,
            ReissueCount = 0,
            MaxReissueCount = maxReissueCount,
            ReissueBlockCooldownInHours = reissueBlockCooldownInHours,
            IsVerified = false
        };
    }

    public void Reissue(string newCodeHash, int expiresInMinutes, DateTimeOffset now)
    {
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
        NextAllowedReissueAt = ReissueCount == MaxReissueCount? NextAllowedReissueAt.AddHours(ReissueBlockCooldownInHours) : now.AddSeconds(ReissueCooldownSeconds);
    }

    public string? Verify(string codeHash, DateTimeOffset now)
    {
        VerificationAttempts++;

        if (IsVerified)
        {
            return ErrorCodes.OTPAlreadyVerified;
        }

        if (ExpiresAt <= now)
        {
            return ErrorCodes.OTPExpired;
        }

        if (VerificationAttempts > MaxVerificationAttempts)
        {
            return ErrorCodes.OTPReachedMaxAttempts;
        }

        if (!HashesMatch(CodeHash, codeHash))
        {
            return ErrorCodes.OTPNotMatched;
        }

        IsVerified = true;
        VerificationAttempts = 0;
        return null;
    }

    public void MarkUsed(DateTimeOffset now)
    {
        if (!IsVerified)
        {
            throw new BadRequestCoreException(ErrorCodes.OTPNotVerified);
        }

        if (IsUsed)
        {
            throw new BadRequestCoreException(ErrorCodes.OTPAlreadyUsed);
        }

        if (ExpiresAt <= now)
        {
            throw new BadRequestCoreException(ErrorCodes.OTPExpired);
        }

        IsUsed = true;
    }

    private static bool HashesMatch(string expected, string actual) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
