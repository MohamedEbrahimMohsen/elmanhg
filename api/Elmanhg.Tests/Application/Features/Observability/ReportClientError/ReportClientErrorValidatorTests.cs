using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Observability.ReportClientError;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Observability.ReportClientError;

public sealed class ReportClientErrorValidatorTests
{
    private static readonly ClientErrorsOptions Limits = new();
    private readonly ReportClientErrorValidator _validator = new(Options.Create(Limits));

    [Fact]
    public void Validate_ValidReport_Passes()
    {
        _validator.Validate(Report()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyMessage_FailsWithMessageRequired()
    {
        Codes(Report() with { Message = "" }).Should().Contain(ErrorCodes.ClientErrorMessageRequired);
    }

    [Fact]
    public void Validate_MessageTooLong_FailsWithMessageTooLong()
    {
        Codes(Report() with { Message = new string('m', Limits.MessageMaxLength + 1) }).Should().Contain(ErrorCodes.ClientErrorMessageTooLong);
    }

    [Fact]
    public void Validate_ErrorNameTooLong_FailsWithNameTooLong()
    {
        Codes(Report() with { ErrorName = new string('n', Limits.ErrorNameMaxLength + 1) }).Should().Contain(ErrorCodes.ClientErrorNameTooLong);
    }

    [Fact]
    public void Validate_StackTooLong_FailsWithStackTooLong()
    {
        Codes(Report() with { Stack = new string('s', Limits.StackMaxLength + 1) }).Should().Contain(ErrorCodes.ClientErrorStackTooLong);
    }

    [Fact]
    public void Validate_PathTooLong_FailsWithPathTooLong()
    {
        Codes(Report() with { Path = "/" + new string('p', Limits.PathMaxLength) }).Should().Contain(ErrorCodes.ClientErrorPathTooLong);
    }

    [Fact]
    public void Validate_PathWithoutLeadingSlash_FailsWithPathInvalid()
    {
        Codes(Report() with { Path = "https://evil.example/student" }).Should().Contain(ErrorCodes.ClientErrorPathInvalid);
    }

    [Fact]
    public void Validate_UnknownSource_FailsWithSourceInvalid()
    {
        Codes(Report() with { Source = (ClientErrorSource)99 }).Should().Contain(ErrorCodes.ClientErrorSourceInvalid);
    }

    [Fact]
    public void Validate_NullOptionalFields_Passes()
    {
        _validator.Validate(Report() with { ErrorName = null, Stack = null, Path = null }).IsValid.Should().BeTrue();
    }

    private static ReportClientErrorCommand Report() => new("TypeError: x is undefined", "TypeError", "at App (main.js:1:1)", ClientErrorSource.Window, "/student");

    private List<string> Codes(ReportClientErrorCommand command) => _validator.Validate(command).Errors.Select(x => x.ErrorCode).ToList();
}
