using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Core.OTP.Repositories;
using Elmanhg.Application.Auth.LoginWithPhone;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using System.Security.Claims;
using OtpErrorCodes = Core.OTP.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Auth.LoginWithPhone;

public sealed class LoginWithPhoneHandlerTests
{
    private const string PhoneNumber = "01012345678";

    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenService<User, Guid> _refreshTokenService = Substitute.For<IRefreshTokenService<User, Guid>>();
    private readonly LoginWithPhoneHandler _handler;

    public LoginWithPhoneHandlerTests()
    {
        _tokenService.GenerateTokenAsync(Arg.Any<List<Claim>>()).Returns("access-token");
        _refreshTokenService.GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("refresh-token");
        _handler = new LoginWithPhoneHandler(_userManager, _otpRepository, _tokenService, _refreshTokenService);
    }

    [Fact]
    public async Task Handle_RegisteredActiveUser_MarksOtpUsedAndReturnsTokens()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone(PhoneNumber).Verified());
        var user = User.CreateStudentWithPhone("Ahmed", PhoneNumber);
        _userManager.FindByNameAsync(PhoneNumber).Returns(user);

        var result = await _handler.Handle(new LoginWithPhoneCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        otp.IsUsed.Should().BeTrue();
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        result.User.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task Handle_UnknownVerificationId_ThrowsOtpInvalid()
    {
        var act = () => _handler.Handle(new LoginWithPhoneCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpInvalid);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnverifiedOtp_ThrowsOtpNotVerified()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone(PhoneNumber));

        var act = () => _handler.Handle(new LoginWithPhoneCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(OtpErrorCodes.OTPNotVerified);
        await _userManager.DidNotReceive().FindByNameAsync(Arg.Any<string>());
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnregisteredPhone_ThrowsNotFound()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone(PhoneNumber).Verified());

        var act = () => _handler.Handle(new LoginWithPhoneCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PhoneNumberNotRegistered);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuspendedUser_ThrowsForbidden()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone(PhoneNumber).Verified());
        var user = User.CreateStudentWithPhone("Ahmed", PhoneNumber);
        user.Suspend();
        _userManager.FindByNameAsync(PhoneNumber).Returns(user);

        var act = () => _handler.Handle(new LoginWithPhoneCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserSuspended);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmailOtp_ThrowsOtpInvalid()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail("mona@elmanhg.test").Verified());

        var act = () => _handler.Handle(new LoginWithPhoneCommand(otp.VerificationId), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpInvalid);
        await _userManager.DidNotReceive().FindByNameAsync(Arg.Any<string>());
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private global::Core.OTP.Entities.Otp ArrangeOtp(OtpBuilder builder)
    {
        var otp = builder.Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);
        return otp;
    }
}
