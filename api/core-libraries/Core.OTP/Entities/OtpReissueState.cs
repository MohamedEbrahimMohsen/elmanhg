namespace Core.OTP.Entities;

public sealed record OtpReissueState(Guid VerificationId, string CodeHash, bool IsVerified, bool IsUsed, int VerificationAttempts, int ReissueCount, DateTimeOffset ReissueWindowStartedAt, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset NextAllowedReissueAt);
