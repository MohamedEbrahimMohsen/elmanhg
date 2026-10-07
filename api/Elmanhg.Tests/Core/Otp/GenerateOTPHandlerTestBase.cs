using Core.OTP;
using Core.OTP.Delivery;
using Core.OTP.Entities;
using Core.OTP.GenerateOTP;
using Core.OTP.OtpHasher;
using Core.OTP.Repositories;
using Core.Utilities.Generator;
using Elmanhg.Tests.Builders;
using Microsoft.Extensions.Options;
using NSubstitute;
using OtpEntity = Core.OTP.Entities.Otp;

namespace Elmanhg.Tests.Core.Otp;

public abstract class GenerateOTPHandlerTestBase
{
    protected const string PhoneNumber = "01012345678";
    protected const string GeneratedCode = "123456";
    protected static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    protected readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();
    protected readonly IGenerator _generator = Substitute.For<IGenerator>();
    protected readonly IOtpHasher _otpHasher = Substitute.For<IOtpHasher>();
    protected readonly IOtpSender _otpSender = Substitute.For<IOtpSender>();
    protected readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    protected readonly GenerateOTPHandler _handler;

    protected GenerateOTPHandlerTestBase()
    {
        _generator.Generate(Arg.Any<int>(), Arg.Any<string>()).Returns(GeneratedCode);
        _otpHasher.Hash(GeneratedCode).Returns(OtpBuilder.CodeHash);
        _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(OtpChannel.Sms);
        _timeProvider.GetUtcNow().Returns(Now);
        _otpRepository.AddIfAbsentAsync(Arg.Any<OtpEntity>(), Arg.Any<CancellationToken>()).Returns(true);
        _handler = new GenerateOTPHandler(_otpRepository, _generator, _otpHasher, Options.Create(new OtpOptions()), _otpSender, _timeProvider);
    }
}
