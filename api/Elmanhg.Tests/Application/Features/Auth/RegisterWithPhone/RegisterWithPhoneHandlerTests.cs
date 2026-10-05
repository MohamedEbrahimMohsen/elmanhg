using Core.Errors;
using Core.Identity.Tokens.AccessToken;
using Core.Identity.Tokens.RefreshToken;
using Core.OTP.Repositories;
using Elmanhg.Application.Auth.RegisterWithPhone;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Identity;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using System.Security.Claims;
using OtpErrorCodes = Core.OTP.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Auth.RegisterWithPhone;

public sealed class RegisterWithPhoneHandlerTests
{
    private const string PhoneNumber = "01012345678";
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    private readonly UserManager<User> _userManager = UserManagerSubstitute.Create();
    private readonly IOtpRepository _otpRepository = Substitute.For<IOtpRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IRefreshTokenService<User, Guid> _refreshTokenService = Substitute.For<IRefreshTokenService<User, Guid>>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly RegisterWithPhoneHandler _handler;

    public RegisterWithPhoneHandlerTests()
    {
        _tokenService.GenerateTokenAsync(Arg.Any<List<Claim>>()).Returns("access-token");
        _refreshTokenService.GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()).Returns("refresh-token");
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Success);
        _timeProvider.GetUtcNow().Returns(Now);
        _handler = new RegisterWithPhoneHandler(_userManager, _otpRepository, _tokenService, _refreshTokenService, _timeProvider);
    }

    [Fact]
    public async Task Handle_VerifiedOtpNewPhone_CreatesStudentAndReturnsTokens()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone(PhoneNumber).Verified());

        var result = await _handler.Handle(new RegisterWithPhoneCommand(otp.VerificationId, "Ahmed", TermsVersions.Current), TestContext.Current.CancellationToken);

        await _userManager.Received(1).CreateAsync(Arg.Is<User>(x => x.Role == UserRole.Student && x.PhoneNumber == PhoneNumber));
        otp.IsUsed.Should().BeTrue();
        result.User.Role.Should().Be("Student");
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
    }

    [Fact]
    public async Task Handle_VerifiedOtpNewPhone_RecordsTermsAcceptance()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone(PhoneNumber).Verified());

        await _handler.Handle(new RegisterWithPhoneCommand(otp.VerificationId, "Ahmed", TermsVersions.Current), TestContext.Current.CancellationToken);

        await _userManager.Received(1).CreateAsync(Arg.Is<User>(x => x.TermsVersion == TermsVersions.Current && x.TermsAcceptedAt == Now));
    }

    [Fact]
    public async Task Handle_UnknownVerificationId_ThrowsOtpInvalid()
    {
        var act = () => _handler.Handle(new RegisterWithPhoneCommand(Guid.NewGuid(), "Ahmed", TermsVersions.Current), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpInvalid);
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_UnverifiedOtp_ThrowsOtpNotVerified()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone(PhoneNumber));

        var act = () => _handler.Handle(new RegisterWithPhoneCommand(otp.VerificationId, "Ahmed", TermsVersions.Current), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(OtpErrorCodes.OTPNotVerified);
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_PhoneAlreadyRegistered_ThrowsConflict()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone(PhoneNumber).Verified());
        _userManager.FindByNameAsync(PhoneNumber).Returns(User.CreateStudentWithPhone("Existing", PhoneNumber));

        var act = () => _handler.Handle(new RegisterWithPhoneCommand(otp.VerificationId, "Ahmed", TermsVersions.Current), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PhoneNumberAlreadyRegistered);
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task Handle_IdentityRejectsUser_ThrowsUserCreationFailed()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForPhone(PhoneNumber).Verified());
        _userManager.CreateAsync(Arg.Any<User>()).Returns(IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName" }));

        var act = () => _handler.Handle(new RegisterWithPhoneCommand(otp.VerificationId, "Ahmed", TermsVersions.Current), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserCreationFailed);
        await _refreshTokenService.DidNotReceive().GenerateTokenAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmailOtp_ThrowsOtpInvalid()
    {
        var otp = ArrangeOtp(new OtpBuilder().ForEmail("mona@elmanhg.test").Verified());

        var act = () => _handler.Handle(new RegisterWithPhoneCommand(otp.VerificationId, "Ahmed", TermsVersions.Current), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.OtpInvalid);
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<User>());
    }

    private global::Core.OTP.Entities.Otp ArrangeOtp(OtpBuilder builder)
    {
        var otp = builder.Build();
        _otpRepository.FindByVerificationId(otp.VerificationId, Arg.Any<CancellationToken>()).Returns(otp);
        return otp;
    }
}
