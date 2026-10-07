using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Core.OTP.Repositories;
using Elmanhg.Application.Auth.AcceptInvitation;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using System.Security.Claims;
using OtpErrorCodes = Core.OTP.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Auth.AcceptInvitation;

public sealed class AcceptInvitationHandlerTests
{
    private const string Email = "teacher@elmanhg.test";
    private const string Password = "Password1";
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenService<User, Guid> _refreshTokenService = Substitute.For<IRefreshTokenService<User, Guid>>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly AcceptInvitationHandler _handler;

    public AcceptInvitationHandlerTests()
    {
        _tokenService.GenerateTokenAsync(Arg.Any<List<Claim>>()).Returns("access-token");
        _refreshTokenService.GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("refresh-token");
        _userManager.AddPasswordAsync(Arg.Any<User>(), Arg.Any<string>()).Returns(IdentityResult.Success);
        _timeProvider.GetUtcNow().Returns(Now);
        _handler = new AcceptInvitationHandler(_userManager, _otpRepository, _tokenService, _refreshTokenService, _timeProvider);
    }

    [Fact]
    public async Task Handle_PendingInvitation_AddsPasswordAndReturnsTokens()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email).Verified());
        var teacher = ArrangeUser(User.CreateTeacher("Teacher", Email));

        var result = await _handler.Handle(new AcceptInvitationCommand(otp.VerificationId, Password), TestContext.Current.CancellationToken);

        await _userManager.Received(1).AddPasswordAsync(teacher, Password);
        otp.IsUsed.Should().BeTrue();
        await _otpRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        (result.User.Role, result.AccessToken, result.RefreshToken).Should().Be((UserRole.Teacher.ToString(), "access-token", "refresh-token"));
    }

    [Fact]
    public async Task Handle_UnknownVerification_ThrowsOtpInvalid()
    {
        var act = () => _handler.Handle(new AcceptInvitationCommand(Guid.NewGuid(), Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpInvalid);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PhoneOtp_ThrowsOtpInvalid()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone("01012345678").Verified());

        var act = () => _handler.Handle(new AcceptInvitationCommand(otp.VerificationId, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpInvalid);
        otp.IsUsed.Should().BeFalse();
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnverifiedOtp_ThrowsOtpNotVerified()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email));
        ArrangeUser(User.CreateTeacher("Teacher", Email));

        var act = () => _handler.Handle(new AcceptInvitationCommand(otp.VerificationId, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(OtpErrorCodes.OTPNotVerified);
        await _userManager.DidNotReceive().AddPasswordAsync(Arg.Any<User>(), Arg.Any<string>());
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoUserForEmail_ThrowsInvitationNotFound()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email).Verified());

        var act = () => _handler.Handle(new AcceptInvitationCommand(otp.VerificationId, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.InvitationNotFound);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserAlreadyHasPassword_ThrowsInvitationNotFound()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email).Verified());
        var teacher = User.CreateTeacher("Teacher", Email);
        teacher.PasswordHash = "hash";
        ArrangeUser(teacher);

        var act = () => _handler.Handle(new AcceptInvitationCommand(otp.VerificationId, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.InvitationNotFound);
        await _userManager.DidNotReceive().AddPasswordAsync(Arg.Any<User>(), Arg.Any<string>());
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StudentEmail_ThrowsInvitationNotFound()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email).Verified());
        ArrangeUser(User.CreateStudentWithEmail("Student", Email));

        var act = () => _handler.Handle(new AcceptInvitationCommand(otp.VerificationId, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.InvitationNotFound);
        await _userManager.DidNotReceive().AddPasswordAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_SuspendedInvitee_ThrowsUserSuspended()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email).Verified());
        var teacher = User.CreateTeacher("Teacher", Email);
        teacher.Suspend();
        ArrangeUser(teacher);

        var act = () => _handler.Handle(new AcceptInvitationCommand(otp.VerificationId, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserSuspended);
        await _userManager.DidNotReceive().AddPasswordAsync(Arg.Any<User>(), Arg.Any<string>());
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PasswordRejectedByIdentity_ThrowsPasswordRejected()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail(Email).Verified());
        var teacher = ArrangeUser(User.CreateTeacher("Teacher", Email));
        _userManager.AddPasswordAsync(teacher, Password).Returns(IdentityResult.Failed(new IdentityError { Code = "PasswordRequiresUniqueChars" }));

        var act = () => _handler.Handle(new AcceptInvitationCommand(otp.VerificationId, Password), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PasswordRejected);
        await _otpRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private global::Core.OTP.Entities.Otp ArrangeOtp(OtpBuilder builder)
    {
        var otp = builder.IssuedAt(Now).Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);
        return otp;
    }

    private User ArrangeUser(User user)
    {
        _userManager.FindByEmailAsync(Email).Returns(user);
        return user;
    }
}
