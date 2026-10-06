using Core.OTP.Entities;

namespace Elmanhg.Tests.Builders;

public sealed class OtpBuilder
{
    public const string CodeHash = "code-hash";

    private string _recipient = "01012345678";
    private OtpRecipientType _recipientType = OtpRecipientType.Phone;
    private bool _verified = false;
    private int _reissueCooldownSeconds = 60;
    private int _maxReissueCount = 5;
    private int _maxVerificationAttempts = 3;
    private int _reissueBlockCooldownInHours = 24;
    private DateTimeOffset _issuedAt = DateTimeOffset.UtcNow;

    public OtpBuilder ForPhone(string phoneNumber)
    {
        _recipient = phoneNumber;
        _recipientType = OtpRecipientType.Phone;
        return this;
    }

    public OtpBuilder ForEmail(string email)
    {
        _recipient = email;
        _recipientType = OtpRecipientType.Email;
        return this;
    }

    public OtpBuilder Verified()
    {
        _verified = true;
        return this;
    }

    public OtpBuilder WithReissueCooldownSeconds(int reissueCooldownSeconds)
    {
        _reissueCooldownSeconds = reissueCooldownSeconds;
        return this;
    }

    public OtpBuilder WithMaxReissueCount(int maxReissueCount)
    {
        _maxReissueCount = maxReissueCount;
        return this;
    }

    public OtpBuilder WithMaxVerificationAttempts(int maxVerificationAttempts)
    {
        _maxVerificationAttempts = maxVerificationAttempts;
        return this;
    }

    public OtpBuilder WithReissueBlockCooldownInHours(int reissueBlockCooldownInHours)
    {
        _reissueBlockCooldownInHours = reissueBlockCooldownInHours;
        return this;
    }

    public OtpBuilder IssuedAt(DateTimeOffset issuedAt)
    {
        _issuedAt = issuedAt;
        return this;
    }

    public Otp Build()
    {
        var otp = Otp.Create(_recipientType, _recipient, CodeHash, 5, _maxVerificationAttempts, _reissueCooldownSeconds, _maxReissueCount, _reissueBlockCooldownInHours, _issuedAt);
        if (_verified)
        {
            otp.Verify(CodeHash);
        }

        return otp;
    }
}
