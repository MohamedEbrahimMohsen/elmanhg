using System.ComponentModel.DataAnnotations;

namespace Core.OTP;

public class OtpOptions
{
    public const string SectionName = "CoreOtp";

    public string Secret { get; set; } = default!;
    public int OtpLength { get; init; } = 6;
    [MinLength(1)]
    public List<string> PhoneCodes { get; set; } = [];
    [Range(1, int.MaxValue)]
    public int PhoneLength { get; set; }
    public int EmailMaxLength { get; init; } = 256;
    public string AllowedCharacters { get; init; } = "0123456789";
    public int ExpirationMinutes { get; init; } = 5;
    public int MaxVerificationAttempts { get; init; } = 3;
    public int ReissueCooldownSeconds { get; init; } = 60;
    public int MaxReissueCount { get; init; } = 5;
    public int ReissueBlockCooldownInHours { get; init; } = 24;
}