using Core.Errors;
using Core.OTP.Entities;
using Core.OTP.Exceptions;
using Core.OTP.Repositories;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Core.Otp;

public sealed class OtpRepositoryExtensionsTests
{
    private const string InvalidCode = "OTP_PROBE";
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();

    [Fact]
    public async Task ConsumeAsync_VerifiedMatchingType_MarksUsedAndReturnsOtp()
    {
        var otp = new OtpBuilder().ForEmail("student@example.com").Verified().IssuedAt(Now).Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);

        var result = await _otpRepository.ConsumeAsync(otp.VerificationId, OtpRecipientType.Email, InvalidCode, Now, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(otp);
        result.IsUsed.Should().BeTrue();
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsumeAsync_UnknownVerificationId_ThrowsBadRequestWithGivenCode()
    {
        var act = () => _otpRepository.ConsumeAsync(Guid.NewGuid(), OtpRecipientType.Phone, InvalidCode, Now, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(InvalidCode);
    }

    [Fact]
    public async Task ConsumeAsync_OtherRecipientType_ThrowsBadRequestWithGivenCode()
    {
        var otp = new OtpBuilder().ForPhone("01012345678").Verified().IssuedAt(Now).Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);

        var act = () => _otpRepository.ConsumeAsync(otp.VerificationId, OtpRecipientType.Email, InvalidCode, Now, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(InvalidCode);
        otp.IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task ConsumeAsync_NotVerified_ThrowsOtpNotVerified()
    {
        var otp = new OtpBuilder().ForPhone("01012345678").IssuedAt(Now).Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);

        var act = () => _otpRepository.ConsumeAsync(otp.VerificationId, OtpRecipientType.Phone, InvalidCode, Now, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OTPNotVerified);
    }

    [Fact]
    public async Task ConsumeAsync_ExpiredAtGivenTime_ThrowsOtpExpired()
    {
        var otp = new OtpBuilder().ForPhone("01012345678").Verified().IssuedAt(Now).Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);

        var act = () => _otpRepository.ConsumeAsync(otp.VerificationId, OtpRecipientType.Phone, InvalidCode, Now.AddMinutes(5), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OTPExpired);
    }
}
