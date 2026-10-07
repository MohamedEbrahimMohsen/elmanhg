using Core.Errors;
using Core.OTP.Entities;
using Core.OTP.Exceptions;
using Core.OTP.GenerateOTP;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using OtpEntity = Core.OTP.Entities.Otp;

namespace Elmanhg.Tests.Core.Otp;

public sealed class GenerateOTPHandlerInsertRaceTests : GenerateOTPHandlerTestBase
{
    [Fact]
    public async Task Handle_LostInsertRaceWithinCooldown_ThrowsCooldownAndSendsNothing()
    {
        _otpRepository.AddIfAbsentAsync(Arg.Any<OtpEntity>(), Arg.Any<CancellationToken>()).Returns(false);
        _otpRepository.FindByRecipientAsync(PhoneNumber, Arg.Any<CancellationToken>()).Returns(null, new OtpBuilder().ForPhone(PhoneNumber).IssuedAt(Now).Build());

        var act = () => _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<RateLimitExceededCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OTPReissueCooldown);
        await _otpSender.DidNotReceive().SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LostInsertRacePastCooldown_ReissuesTheWinnersRowAndSends()
    {
        var winner = new OtpBuilder().ForPhone(PhoneNumber).IssuedAt(Now.AddMinutes(-2)).Build();
        _otpRepository.AddIfAbsentAsync(Arg.Any<OtpEntity>(), Arg.Any<CancellationToken>()).Returns(false);
        _otpRepository.FindByRecipientAsync(PhoneNumber, Arg.Any<CancellationToken>()).Returns(null, winner);

        var result = await _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        result.ReissueCount.Should().Be(1);
        result.VerificationId.Should().Be(winner.VerificationId);
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _otpSender.Received(1).SendAsync(OtpRecipientType.Phone, PhoneNumber, GeneratedCode, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LostInsertRaceAndWinnerGone_ThrowsOtpModifiedConcurrently()
    {
        _otpRepository.AddIfAbsentAsync(Arg.Any<OtpEntity>(), Arg.Any<CancellationToken>()).Returns(false);
        _otpRepository.FindByRecipientAsync(PhoneNumber, Arg.Any<CancellationToken>()).Returns(null, (OtpEntity?)null);

        var act = () => _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpModifiedConcurrently);
        await _otpSender.DidNotReceive().SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
