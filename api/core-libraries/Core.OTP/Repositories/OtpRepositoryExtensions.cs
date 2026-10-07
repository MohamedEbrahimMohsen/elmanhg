using Core.Errors;
using Core.OTP.Entities;

namespace Core.OTP.Repositories;

public static class OtpRepositoryExtensions
{
    public static async Task<Otp> ConsumeAsync(this IOtpRepository otpRepository, Guid verificationId, OtpRecipientType recipientType, string invalidErrorCode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var otp = await otpRepository.FindByVerificationId(verificationId, cancellationToken).ConfigureAwait(false);
        if (otp is null || otp.RecipientType != recipientType)
        {
            throw new BadRequestCoreException(invalidErrorCode);
        }

        otp.MarkUsed(now);
        return otp;
    }
}
