using Core.Errors;
using Core.OTP;
using Core.OTP.Delivery;
using Core.OTP.Entities;
using Core.OTP.Exceptions;
using Core.OTP.GenerateOTP;
using Core.OTP.OtpHasher;
using Core.OTP.Repositories;
using Core.Utilities.Generator;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using AppErrorCodes = Elmanhg.Application.Exceptions.ErrorCodes;
using OtpEntity = Core.OTP.Entities.Otp;

namespace Elmanhg.Tests.Core.Otp;

public sealed class GenerateOTPHandlerTests
{
    private const string PhoneNumber = "01012345678";
    private const string GeneratedCode = "123456";
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();
    private readonly IGenerator _generator = Substitute.For<IGenerator>();
    private readonly IOtpHasher _otpHasher = Substitute.For<IOtpHasher>();
    private readonly IOtpSender _otpSender = Substitute.For<IOtpSender>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly GenerateOTPHandler _handler;

    public GenerateOTPHandlerTests()
    {
        _generator.Generate(Arg.Any<int>(), Arg.Any<string>()).Returns(GeneratedCode);
        _otpHasher.Hash(GeneratedCode).Returns(OtpBuilder.CodeHash);
        _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(OtpChannel.Sms);
        _timeProvider.GetUtcNow().Returns(Now);
        _handler = new GenerateOTPHandler(_otpRepository, _generator, _otpHasher, Options.Create(new OtpOptions()), _otpSender, _timeProvider);
    }

    [Fact]
    public async Task Handle_NewPhone_CreatesOtpAndSendsGeneratedCode()
    {
        var result = await _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        result.VerificationId.Should().NotBeEmpty();
        result.Channel.Should().Be(OtpChannel.Sms);
        await _otpRepository.Received(1).AddAsync(Arg.Is<OtpEntity>(x => x.Recipient == PhoneNumber && x.RecipientType == OtpRecipientType.Phone), Arg.Any<CancellationToken>());
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _otpSender.Received(1).SendAsync(OtpRecipientType.Phone, PhoneNumber, GeneratedCode, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewEmail_CreatesEmailOtpWithNormalizedRecipient()
    {
        await _handler.Handle(new GenerateOTPCommand(null, "  Mona@Elmanhg.Test "), TestContext.Current.CancellationToken);

        await _otpRepository.Received(1).AddAsync(Arg.Is<OtpEntity>(x => x.Recipient == "mona@elmanhg.test" && x.RecipientType == OtpRecipientType.Email), Arg.Any<CancellationToken>());
        await _otpSender.Received(1).SendAsync(OtpRecipientType.Email, "mona@elmanhg.test", GeneratedCode, Arg.Any<CancellationToken>());
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingOtpInCooldown_ThrowsRateLimitAndSendsNothing()
    {
        _otpRepository.FindByRecipientAsync(PhoneNumber, Arg.Any<CancellationToken>()).Returns(new OtpBuilder().ForPhone(PhoneNumber).IssuedAt(Now).Build());

        var act = () => _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<RateLimitExceededCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OTPReissueCooldown);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _otpSender.DidNotReceive().SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NewPhone_StampsTimesFromTimeProvider()
    {
        var result = await _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        result.ExpiresAt.Should().Be(Now.AddMinutes(5));
        result.NextAllowedReissueAt.Should().Be(Now.AddSeconds(60));
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingOtpPastCooldown_ReissuesWithoutAdding()
    {
        _otpRepository.FindByRecipientAsync(PhoneNumber, Arg.Any<CancellationToken>()).Returns(new OtpBuilder().ForPhone(PhoneNumber).IssuedAt(Now.AddMinutes(-2)).Build());

        var result = await _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        result.ReissueCount.Should().Be(1);
        await _otpRepository.DidNotReceive().AddAsync(Arg.Any<OtpEntity>(), Arg.Any<CancellationToken>());
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _otpSender.Received(1).SendAsync(OtpRecipientType.Phone, PhoneNumber, GeneratedCode, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DeliveryUnavailable_ThrowsAndSavesNothing()
    {
        _otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(AppErrorCodes.OtpChannelUnavailable));

        var act = () => _handler.Handle(new GenerateOTPCommand(PhoneNumber), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(AppErrorCodes.OtpChannelUnavailable);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
