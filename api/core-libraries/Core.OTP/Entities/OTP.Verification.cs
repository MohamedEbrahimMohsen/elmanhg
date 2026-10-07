using Core.Errors;
using Core.OTP.Exceptions;
using System.Security.Cryptography;
using System.Text;

namespace Core.OTP.Entities;

public partial class Otp
{
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
