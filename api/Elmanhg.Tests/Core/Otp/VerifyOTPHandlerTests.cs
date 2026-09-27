using Core.Errors;
using Core.OTP.Exceptions;
using Core.OTP.OtpHasher;
using Core.OTP.Repositories;
using Core.OTP.VerifyOTP;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Core.Otp;

public sealed class VerifyOTPHandlerTests
{
    private const string CorrectCode = "123456";
    private const string WrongCode = "654321";

    private readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();
    private readonly IOtpHasher _otpHasher = Substitute.For<IOtpHasher>();
    private readonly VerifyOTPHandler _handler;

    public VerifyOTPHandlerTests()
    {
        _otpHasher.Hash(CorrectCode).Returns(OtpBuilder.CodeHash);
        _otpHasher.Hash(WrongCode).Returns("wrong-hash");
        _handler = new VerifyOTPHandler(_otpRepository, _otpHasher);
    }

    [Fact]
    public async Task Handle_UnknownVerificationId_ThrowsBadRequestOtpInvalid()
    {
        var act = () => _handler.Handle(new VerifyOTPCommand(CorrectCode, Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpInvalid);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WrongCode_SavesAttemptAndThrowsOtpNotMatched()
    {
        var otp = new OtpBuilder().Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);

        var act = () => _handler.Handle(new VerifyOTPCommand(WrongCode, otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OTPNotMatched);
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AttemptsExhausted_ThrowsRateLimitExceeded()
    {
        var otp = new OtpBuilder().WithMaxVerificationAttempts(0).Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);

        var act = () => _handler.Handle(new VerifyOTPCommand(CorrectCode, otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<RateLimitExceededCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OTPReachedMaxAttempts);
    }

    [Fact]
    public async Task Handle_CorrectCode_VerifiesAndSaves()
    {
        var otp = new OtpBuilder().Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);

        await _handler.Handle(new VerifyOTPCommand(CorrectCode, otp.VerificationId), TestContext.Current.CancellationToken);

        otp.IsVerified.Should().BeTrue();
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
