using Core.OTP.Delivery;
using Core.OTP.Entities;
using Core.OTP.Repositories;

namespace Core.OTP.GenerateOTP;

// The row is saved before the paid send, so a parallel request loses on the row version and sends nothing. Only a failure while the request is still live hands the claim back: once the request is cancelled the provider may already have accepted the message, so the cooldown and the resend count stay spent.
public sealed class OtpDeliveryClaim(Otp otp, OtpReissueState? previous)
{
    public Otp Otp => otp;

    public async Task<OtpChannel> DeliverAsync(IOtpSender otpSender, IOtpRepository otpRepository, string code, CancellationToken cancellationToken)
    {
        try
        {
            return await otpSender.SendAsync(otp.RecipientType, otp.Recipient, code, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await ReleaseAsync(otpRepository).ConfigureAwait(false);
            throw;
        }
    }

    private async Task ReleaseAsync(IOtpRepository otpRepository)
    {
        if (previous is null)
        {
            otpRepository.Delete(otp);
        }
        else
        {
            otp.RestoreReissue(previous);
        }

        // Not the request token: a release that has started must finish even if the client goes away meanwhile.
        await otpRepository.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
