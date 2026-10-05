using Elmanhg.Application.Auth.RegisterWithPhone;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Auth.RegisterWithPhone;

public sealed class RegisterWithPhoneValidatorTests
{
    private readonly RegisterWithPhoneValidator _validator = new(Options.Create(new AuthOptions { DisplayNameMaxLength = 100 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.NewGuid(), "Ahmed", TermsVersions.Current));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyVerificationId_FailsWithVerificationIdInvalidFormat()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.Empty, "Ahmed", TermsVersions.Current));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.OtpVerificationIdInvalidFormat);
    }

    [Fact]
    public void Validate_EmptyDisplayName_FailsWithDisplayNameRequired()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.NewGuid(), string.Empty, TermsVersions.Current));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.DisplayNameRequired);
    }

    [Fact]
    public void Validate_DisplayNameOverMax_FailsWithDisplayNameTooLong()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.NewGuid(), new string('a', 101), TermsVersions.Current));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.DisplayNameTooLong);
    }

    [Fact]
    public void Validate_EmptyTermsVersion_FailsWithTermsVersionRequired()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.NewGuid(), "Ahmed", string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TermsVersionRequired);
    }

    [Fact]
    public void Validate_EmptyTermsVersion_DoesNotReportUnknownVersion()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.NewGuid(), "Ahmed", string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().NotContain(DomainErrorCodes.TermsVersionUnknown);
    }

    [Fact]
    public void Validate_UnknownTermsVersion_FailsWithTermsVersionUnknown()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.NewGuid(), "Ahmed", "2020-01-01"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(DomainErrorCodes.TermsVersionUnknown);
    }
}
