using Core.Errors;
using Core.OTP;
using Core.OTP.Exceptions;
using Core.OTP.GenerateOTP;
using Core.OTP.OtpHasher;
using Core.OTP.Repositories;
using Core.OTP.Sms;
using Core.Utilities.Generator;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using OtpEntity = Core.OTP.Entities.Otp;

namespace Elmanhg.Tests.Core.Otp;

public sealed class GenerateOTPHandlerTests
{
    private const string PhoneNumber = "01012345678";
    private const string GeneratedCode = "123456";

    private readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly IOtpHasher _otpHasher = Substitute.For<IOtpHasher>();
    private readonly ISmsSender _smsSender = Substitute.For<ISmsSender>();
    private readonly GenerateOTPHandler _handler;

    public GenerateOTPHandlerTests()
    {
        _generator.Generate(Arg.Any<int>(), Arg.Any<string>()).Returns(GeneratedCode);
        _otpHasher.Hash(GeneratedCode).Returns(OtpBuilder.CodeHash);
        _handler = new GenerateOTPHandler(_otpRepository, _generator, _otpHasher, Options.Create(new OtpOptions()), _smsSender);
    }

    [Fact]
    public async Task Handle_NewPhone_CreatesOtpAndSendsGeneratedCode()
    {
        var result = await _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        result.VerificationId.Should().NotBeEmpty();
        await _otpRepository.Received(1).AddAsync(Arg.Is<OtpEntity>(x => x.PhoneNumber == PhoneNumber), Arg.Any<CancellationToken>());
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _smsSender.Received(1).SendOtpAsync(PhoneNumber, GeneratedCode, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingOtpInCooldown_ThrowsRateLimitAndSendsNothing()
    {
        _otpRepository.FindAsync(PhoneNumber, Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(new OtpBuilder().ForPhone(PhoneNumber).Build());

        var act = () => _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<RateLimitExceededCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OTPReissueCooldown);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _smsSender.DidNotReceive().SendOtpAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
