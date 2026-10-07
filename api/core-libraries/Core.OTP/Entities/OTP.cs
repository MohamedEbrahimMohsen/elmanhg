using Core.DDD.Entities;

namespace Core.OTP.Entities;

public partial class Otp : Entity, IVersioned
{
    public Guid VerificationId { get; private set; }
    public string Recipient { get; private set; }
    public OtpRecipientType RecipientType { get; private set; }
    public string CodeHash { get; private set; }

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
    public uint Version { get; private set; }

    private Otp(Guid id) : base(id) { }

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
}
