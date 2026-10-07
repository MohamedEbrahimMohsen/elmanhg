using Core.Errors;
using Core.OTP.Delivery;
using Core.OTP.Entities;
using Core.OTP.Exceptions;
using Core.OTP.OtpHasher;
using Core.OTP.Repositories;
using Core.Utilities.Generator;
using MediatR;
using Microsoft.Extensions.Options;

namespace Core.OTP.GenerateOTP;

public sealed class GenerateOTPHandler(IOtpRepository otpRepository, IGenerator generator, IOtpHasher otpHasher, IOptions<OtpOptions> otpOptions, IOtpSender otpSender, TimeProvider timeProvider) : IRequestHandler<GenerateOTPCommand, GenerateOTPResult>
{
    private readonly OtpOptions _otpOptions = otpOptions.Value;
    public async Task<GenerateOTPResult> Handle(GenerateOTPCommand request, CancellationToken cancellationToken)
    {
        var (recipientType, recipient) = request.Email is { } email ? (OtpRecipientType.Email, email.Trim().ToLowerInvariant()) : (OtpRecipientType.Phone, request.PhoneNumber ?? string.Empty);
        var now = timeProvider.GetUtcNow();
        var code = generator.Generate(size: _otpOptions.OtpLength, allowedCharacters: _otpOptions.AllowedCharacters);
        var codeHash = otpHasher.Hash(code);

        var claim = await ClaimAsync(recipientType, recipient, codeHash, now, cancellationToken).ConfigureAwait(false);
        var channel = await claim.DeliverAsync(otpSender, otpRepository, code, cancellationToken).ConfigureAwait(false);

        var otp = claim.Otp;
        return new GenerateOTPResult(VerificationId: otp.VerificationId,
                                     ExpiresAt: otp.ExpiresAt,
                                     NextAllowedReissueAt: otp.NextAllowedReissueAt,
                                     VerificationAttempts: otp.VerificationAttempts,
                                     ReissueCount: otp.ReissueCount,
                                     MaxVerificationAttempts: otp.MaxVerificationAttempts,
                                     MaxReissueCount: otp.MaxReissueCount,
                                     Channel: channel);
    }

    private async Task<OtpDeliveryClaim> ClaimAsync(OtpRecipientType recipientType, string recipient, string codeHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await otpRepository.FindByRecipientAsync(recipient, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await ReissueAsync(existing, codeHash, now, cancellationToken).ConfigureAwait(false);
        }

        var otp = Otp.Create(recipientType: recipientType,
                             recipient: recipient,
                             codeHash: codeHash,
                             expiresInMinutes: _otpOptions.ExpirationMinutes,
                             maxVerificationAttempts: _otpOptions.MaxVerificationAttempts,
                             reissueCooldownSeconds: _otpOptions.ReissueCooldownSeconds,
                             maxReissueCount: _otpOptions.MaxReissueCount,
                             reissueBlockCooldownInHours: _otpOptions.ReissueBlockCooldownInHours,
                             now: now);
        if (await otpRepository.AddIfAbsentAsync(otp, cancellationToken).ConfigureAwait(false))
        {
            return new OtpDeliveryClaim(otp, previous: null);
        }

        // Another request stored this recipient's row first; its row carries the limits this request must pass.
        var winner = await otpRepository.FindByRecipientAsync(recipient, cancellationToken).ConfigureAwait(false) ?? throw new ConflictCoreException(ErrorCodes.OtpModifiedConcurrently);
        return await ReissueAsync(winner, codeHash, now, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OtpDeliveryClaim> ReissueAsync(Otp otp, string codeHash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var previous = otp.Reissue(codeHash, _otpOptions.ExpirationMinutes, now);
        await otpRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new OtpDeliveryClaim(otp, previous);
    }
}
