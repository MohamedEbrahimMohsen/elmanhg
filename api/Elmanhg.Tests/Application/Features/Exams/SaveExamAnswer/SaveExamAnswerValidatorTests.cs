using Elmanhg.Application.Exams.SaveExamAnswer;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Exams.SaveExamAnswer;

public sealed class SaveExamAnswerValidatorTests
{
    private readonly SaveExamAnswerValidator _validator = new(Options.Create(new SessionsOptions { AnswerMaxLength = 30, EssayAnswerMaxLength = 30 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        _validator.Validate(Command(SessionBuilder.AnswerB)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_FailsWithSessionIdRequired()
    {
        ErrorCodesOf(Command(SessionBuilder.AnswerB) with { SessionId = Guid.Empty }).Should().Contain(ErrorCodes.SessionIdRequired);
    }

    [Fact]
    public void Validate_EmptyQuestionId_FailsWithQuestionIdRequired()
    {
        ErrorCodesOf(Command(SessionBuilder.AnswerB) with { QuestionId = Guid.Empty }).Should().Contain(ErrorCodes.QuestionIdRequired);
    }

    [Fact]
    public void Validate_AnswerNotObject_FailsWithQuestionAnswerInvalid()
    {
        ErrorCodesOf(Command("\"b\"")).Should().Contain(ErrorCodes.QuestionAnswerInvalid);
    }

    [Fact]
    public void Validate_AnswerTooLong_FailsWithAttemptAnswerTooLong()
    {
        ErrorCodesOf(Command("""{"text":"a very long answer that exceeds the cap"}""")).Should().Contain(ErrorCodes.AttemptAnswerTooLong);
    }

    private static SaveExamAnswerCommand Command(string answer) => new(Guid.NewGuid(), Guid.NewGuid(), QuestionBuilder.Json(answer));

    private List<string> ErrorCodesOf(SaveExamAnswerCommand command) => _validator.Validate(command).Errors.Select(x => x.ErrorCode).ToList();
}
