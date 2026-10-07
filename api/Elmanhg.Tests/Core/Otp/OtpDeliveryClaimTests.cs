using Core.Errors;
using Core.OTP.Delivery;
using Core.OTP.Entities;
using Core.OTP.GenerateOTP;
using Core.OTP.Repositories;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using OtpEntity = Core.OTP.Entities.Otp;

namespace Elmanhg.Tests.Core.Otp;

public sealed class OtpDeliveryClaimTests
{
    private const string Code = "123456";
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly IOtpSender _otpSender = Substitute.For<IOtpSender>();
    private readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();

    [Fact]
    public async Task DeliverAsync_SendSucceeds_ReturnsChannelAndSavesNothing()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();
        _otpSender.SendAsync(otp.RecipientType, otp.Recipient, Code, Arg.Any<CancellationToken>()).Returns(OtpChannel.Sms);

        var channel = await new OtpDeliveryClaim(otp, previous: null).DeliverAsync(_otpSender, _otpRepository, Code, TestContext.Current.CancellationToken);

        channel.Should().Be(OtpChannel.Sms);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _otpRepository.DidNotReceive().Delete(Arg.Any<OtpEntity>());
    }

    [Fact]
    public async Task DeliverAsync_NewRowSendFails_DeletesTheRowAndRethrows()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();
        SendThrows(new ServiceUnavailableCoreException("PROBE_DELIVERY_FAILED"));

        var act = () => new OtpDeliveryClaim(otp, previous: null).DeliverAsync(_otpSender, _otpRepository, Code, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be("PROBE_DELIVERY_FAILED");
        _otpRepository.Received(1).Delete(otp);
        await _otpRepository.Received(1).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DeliverAsync_ReissuedRowSendFails_RestoresThePreviousStateAndRethrows()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();
        var (verificationId, nextAllowedReissueAt) = (otp.VerificationId, otp.NextAllowedReissueAt);
        var previous = otp.Reissue("next-hash", 5, Now.AddMinutes(2));
        SendThrows(new ServiceUnavailableCoreException("PROBE_DELIVERY_FAILED"));

        var act = () => new OtpDeliveryClaim(otp, previous).DeliverAsync(_otpSender, _otpRepository, Code, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be("PROBE_DELIVERY_FAILED");
        otp.VerificationId.Should().Be(verificationId);
        otp.ReissueCount.Should().Be(0);
        otp.NextAllowedReissueAt.Should().Be(nextAllowedReissueAt);
        _otpRepository.DidNotReceive().Delete(Arg.Any<OtpEntity>());
        await _otpRepository.Received(1).SaveChangesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        SendThrows(new OperationCanceledException(cancellation.Token));

        var act = () => new OtpDeliveryClaim(otp, previous: null).DeliverAsync(_otpSender, _otpRepository, Code, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        await _otpRepository.Received(1).SaveChangesAsync(CancellationToken.None);
    }

    private void SendThrows(Exception exception) => _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(exception);
}
