using Core.Errors;
using Core.OTP.Delivery;
using Core.OTP.Entities;
using Core.OTP.GenerateOTP;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using AppErrorCodes = Elmanhg.Application.Exceptions.ErrorCodes;
using OtpEntity = Core.OTP.Entities.Otp;

namespace Elmanhg.Tests.Core.Otp;

public sealed class GenerateOTPHandlerDeliveryTests : GenerateOTPHandlerTestBase
{
    [Fact]
    public async Task Handle_DeliveryUnavailable_ThrowsAndDeletesTheClaimedRow()
    {
        _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(AppErrorCodes.OtpChannelUnavailable));

        var act = () => _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(AppErrorCodes.OtpChannelUnavailable);
        await _otpRepository.Received(1).AddIfAbsentAsync(Arg.Any<OtpEntity>(), Arg.Any<CancellationToken>());
        _otpRepository.Received(1).Delete(Arg.Is<OtpEntity>(x => x.Recipient == PhoneNumber));
        await _otpRepository.Received(1).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ExistingOtp_SavesTheClaimBeforeSending()
    {
        _otpRepository.FindByRecipientAsync(PhoneNumber, Arg.Any<CancellationToken>()).Returns(new OtpBuilder().ForPhone(PhoneNumber).IssuedAt(Now.AddMinutes(-2)).Build());

        await _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _otpRepository.SaveChangesAsync(Arg.Any<CancellationToken>());
            _otpSender.SendAsync(OtpRecipientType.Phone, PhoneNumber, GeneratedCode, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ExistingOtpDeliveryFails_RestoresThePreviousCodeAndRethrows()
    {
        var existing = new OtpBuilder().ForPhone(PhoneNumber).IssuedAt(Now.AddMinutes(-2)).Build();
        var (verificationId, nextAllowedReissueAt) = (existing.VerificationId, existing.NextAllowedReissueAt);
        _otpRepository.FindByRecipientAsync(PhoneNumber, Arg.Any<CancellationToken>()).Returns(existing);
        _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(AppErrorCodes.OtpChannelUnavailable));

        var act = () => _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(AppErrorCodes.OtpChannelUnavailable);
        existing.VerificationId.Should().Be(verificationId);
        existing.ReissueCount.Should().Be(0);
        existing.NextAllowedReissueAt.Should().Be(nextAllowedReissueAt);
        await _otpRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RequestCancelledAfterProviderAccepts_KeepsTheNewRowAndRethrows()
    {
        using var cancellation = new CancellationTokenSource();
        _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => { cancellation.Cancel(); return Task.FromException<OtpChannel>(new OperationCanceledException(cancellation.Token)); });

        var act = () => _handler.Handle(new GenerateOTPCommand(PhoneNumber), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _otpRepository.Received(1).AddIfAbsentAsync(Arg.Any<OtpEntity>(), Arg.Any<CancellationToken>());
        _otpRepository.DidNotReceive().Delete(Arg.Any<OtpEntity>());
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RequestCancelledAfterResendAccepted_KeepsTheReissuedRowAndRethrows()
    {
        var existing = new OtpBuilder().ForPhone(PhoneNumber).IssuedAt(Now.AddMinutes(-2)).Build();
        var verificationId = existing.VerificationId;
        _otpRepository.FindByRecipientAsync(PhoneNumber, Arg.Any<CancellationToken>()).Returns(existing);
        using var cancellation = new CancellationTokenSource();
        _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => { cancellation.Cancel(); return Task.FromException<OtpChannel>(new OperationCanceledException(cancellation.Token)); });

        var act = () => _handler.Handle(new GenerateOTPCommand(PhoneNumber), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        existing.ReissueCount.Should().Be(1);
        existing.VerificationId.Should().NotBe(verificationId);
        existing.NextAllowedReissueAt.Should().Be(Now.AddSeconds(60));
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
