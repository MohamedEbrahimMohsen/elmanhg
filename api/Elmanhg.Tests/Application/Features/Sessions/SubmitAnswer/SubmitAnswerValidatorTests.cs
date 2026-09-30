using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.SubmitAnswer;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Elmanhg.Tests.Application.Features.Sessions.SubmitAnswer;

public sealed class SubmitAnswerValidatorTests
{
    private readonly SubmitAnswerValidator _validator = new(Options.Create(new SessionsOptions()));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        _validator.Validate(Command()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoTimeTaken_Passes()
    {
        _validator.Validate(Command() with { TimeTakenMilliseconds = null }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_FailsSessionIdRequired()
    {
        Codes(Command() with { SessionId = Guid.Empty }).Should().Contain(ErrorCodes.SessionIdRequired);
    }

    [Fact]
    public void Validate_EmptyQuestionId_FailsQuestionIdRequired()
    {
        Codes(Command() with { QuestionId = Guid.Empty }).Should().Contain(ErrorCodes.QuestionIdRequired);
    }

    [Theory]
    [InlineData("\"b\"")]
    [InlineData("[]")]
    [InlineData(null)]
    public void Validate_AnswerNotObject_FailsQuestionAnswerInvalid(string? answer)
    {
        var element = answer is null ? default : QuestionBuilder.Json(answer);

        Codes(Command() with { Answer = element }).Should().Contain(ErrorCodes.QuestionAnswerInvalid);
    }

    [Fact]
    public void Validate_AnswerOverMaxLength_FailsAttemptAnswerTooLong()
    {
        var validator = new SubmitAnswerValidator(Options.Create(new SessionsOptions { AnswerMaxLength = 20, EssayAnswerMaxLength = 20, MathStepsAnswerMaxLength = 20 }));

        var codes = validator.Validate(Command() with { Answer = QuestionBuilder.Json("""{"text":"a long answer over twenty"}""") }).Errors.Select(x => x.ErrorCode);

        codes.Should().Contain(ErrorCodes.AttemptAnswerTooLong);
    }

    [Fact]
    public void Validate_NegativeTimeTaken_FailsAttemptTimeTakenInvalid()
    {
        Codes(Command() with { TimeTakenMilliseconds = -1 }).Should().Contain(ErrorCodes.AttemptTimeTakenInvalid);
    }

    private static SubmitAnswerCommand Command() => new(Guid.NewGuid(), Guid.NewGuid(), QuestionBuilder.Json(SessionBuilder.AnswerB), 1000);

    private List<string> Codes(SubmitAnswerCommand command)
    {
        return _validator.Validate(command).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
