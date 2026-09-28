using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.RejectQuestion;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.RejectQuestion;

public sealed class RejectQuestionValidatorTests
{
    private readonly RejectQuestionValidator _validator = new(Options.Create(new QuestionValidationOptions()));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        _validator.Validate(Command()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyQuestionId_FailsQuestionIdRequired()
    {
        Codes(Command() with { QuestionId = Guid.Empty }).Should().Contain(ErrorCodes.QuestionIdRequired);
    }

    [Fact]
    public void Validate_VersionZero_FailsQuestionVersionInvalid()
    {
        Codes(Command() with { Version = 0 }).Should().Contain(ErrorCodes.QuestionVersionInvalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_BlankReason_FailsQuestionRejectionReasonRequired(string? reason)
    {
        Codes(Command() with { Reason = reason }).Should().Contain(DomainErrorCodes.QuestionRejectionReasonRequired);
    }

    [Fact]
    public void Validate_ReasonOverMax_FailsQuestionRejectionReasonTooLong()
    {
        Codes(Command() with { Reason = new string('a', 1001) }).Should().Contain(ErrorCodes.QuestionRejectionReasonTooLong);
    }

    private static RejectQuestionCommand Command() => new(Guid.NewGuid(), 1, "Wrong unit");

    private List<string> Codes(RejectQuestionCommand command)
    {
        return _validator.Validate(command).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
