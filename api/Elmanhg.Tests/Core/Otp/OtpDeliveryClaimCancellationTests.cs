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

public sealed class OtpDeliveryClaimCancellationTests
{
    private const string Code = "123456";
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly IOtpSender _otpSender = Substitute.For<IOtpSender>();
    private readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();

    [Fact]
    public async Task DeliverAsync_SendCancelledByTheRequest_KeepsTheClaimAndRethrows()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException(cancellation.Token));

        var act = () => new OtpDeliveryClaim(otp, previous: null).DeliverAsync(_otpSender, _otpRepository, Code, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _otpRepository.DidNotReceive().Delete(Arg.Any<OtpEntity>());
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliverAsync_RequestCancelledAfterProviderAccepts_ReturnsTheChannelAndKeepsTheClaim()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();
        using var cancellation = new CancellationTokenSource();
        _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => { cancellation.Cancel(); return Task.FromResult(OtpChannel.Sms); });

        var channel = await new OtpDeliveryClaim(otp, previous: null).DeliverAsync(_otpSender, _otpRepository, Code, cancellation.Token);

        channel.Should().Be(OtpChannel.Sms);
        cancellation.IsCancellationRequested.Should().BeTrue();
        _otpRepository.DidNotReceive().Delete(Arg.Any<OtpEntity>());
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeliverAsync_RequestCancelledAfterProviderAcceptsThenSendThrows_KeepsTheReissuedClaim()
    {
        var otp = new OtpBuilder().IssuedAt(Now).Build();
        var verificationId = otp.VerificationId;
        var previous = otp.Reissue("next-hash", 5, Now.AddMinutes(2));
        using var cancellation = new CancellationTokenSource();
        _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => { cancellation.Cancel(); return Task.FromException<OtpChannel>(new OperationCanceledException(cancellation.Token)); });

        var act = () => new OtpDeliveryClaim(otp, previous).DeliverAsync(_otpSender, _otpRepository, Code, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        otp.ReissueCount.Should().Be(1);
        otp.CodeHash.Should().Be("next-hash");
        otp.VerificationId.Should().NotBe(verificationId);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
