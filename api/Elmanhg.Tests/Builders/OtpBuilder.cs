using Core.OTP.Entities;

namespace Elmanhg.Tests.Builders;

public sealed class OtpBuilder
{
    public const string CodeHash = "code-hash";

    private string _phoneNumber = "01012345678";
    private bool _verified = false;
    private int _reissueCooldownSeconds = 60;
    private int _maxReissueCount = 5;
    private int _maxVerificationAttempts = 3;

    public OtpBuilder ForPhone(string phoneNumber)
    {
        _phoneNumber = phoneNumber;
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

    public Otp Build()
    {
        var otp = Otp.Create(_phoneNumber, CodeHash, 5, _maxVerificationAttempts, _reissueCooldownSeconds, _maxReissueCount, 24);
        if (_verified)
        {
            otp.Verify(CodeHash);
        }

        return otp;
    }
}
