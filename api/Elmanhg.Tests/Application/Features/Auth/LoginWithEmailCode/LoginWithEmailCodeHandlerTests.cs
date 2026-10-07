using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Core.OTP.Repositories;
using Elmanhg.Application.Auth.LoginWithEmailCode;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using System.Security.Claims;
using OtpErrorCodes = Core.OTP.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Auth.LoginWithEmailCode;

public sealed class LoginWithEmailCodeHandlerTests
{
    private const string Email = "mona@elmanhg.test";
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenService<User, Guid> _refreshTokenService = Substitute.For<IRefreshTokenService<User, Guid>>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly LoginWithEmailCodeHandler _handler;

    public LoginWithEmailCodeHandlerTests()
    {
        _tokenService.GenerateTokenAsync(Arg.Any<List<Claim>>()).Returns("access-token");
        _refreshTokenService.GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("refresh-token");
        _timeProvider.GetUtcNow().Returns(Now);
        _handler = new LoginWithEmailCodeHandler(_userManager, _otpRepository, _tokenService, _refreshTokenService, _timeProvider);
    }

    [Fact]
    public async Task Handle_RegisteredActiveStudent_MarksOtpUsedAndReturnsTokens()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email).Verified());
        _userManager.FindByEmailAsync(Email).Returns(User.CreateStudentWithEmail("Mona", Email));

        var result = await _handler.Handle(new LoginWithEmailCodeCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        otp.IsUsed.Should().BeTrue();
        result.AccessToken.Should().Be("access-token");
        result.User.Email.Should().Be(Email);
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownVerificationId_ThrowsOtpInvalid()
    {
        var act = () => _handler.Handle(new LoginWithEmailCodeCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpInvalid);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PhoneOtp_ThrowsOtpInvalid()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone("01012345678").Verified());

        var act = () => _handler.Handle(new LoginWithEmailCodeCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpInvalid);
        otp.IsUsed.Should().BeFalse();
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnverifiedOtp_ThrowsOtpNotVerified()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email));

        var act = () => _handler.Handle(new LoginWithEmailCodeCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(OtpErrorCodes.OTPNotVerified);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnregisteredEmail_ThrowsNotFound()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email).Verified());

        var act = () => _handler.Handle(new LoginWithEmailCodeCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.EmailNotRegistered);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StaffAccount_ThrowsForbidden()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email).Verified());
        _userManager.FindByEmailAsync(Email).Returns(User.CreateTeacher("Mona", Email));

        var act = () => _handler.Handle(new LoginWithEmailCodeCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.EmailCodeSignInNotAllowed);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuspendedStudent_ThrowsForbidden()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email).Verified());
        var user = User.CreateStudentWithEmail("Mona", Email);
        user.Suspend();
        _userManager.FindByEmailAsync(Email).Returns(user);

        var act = () => _handler.Handle(new LoginWithEmailCodeCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserSuspended);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private global::Core.OTP.Entities.Otp ArrangeOtp(OtpBuilder builder)
    {
        var otp = builder.IssuedAt(Now).Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);
        return otp;
    }
}
