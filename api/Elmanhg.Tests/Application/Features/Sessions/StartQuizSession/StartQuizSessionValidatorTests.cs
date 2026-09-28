using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.StartQuizSession;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Sessions.StartQuizSession;

public sealed class StartQuizSessionValidatorTests
{
    private readonly StartQuizSessionValidator _validator = new(Options.Create(new SessionsOptions()));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        _validator.Validate(new StartQuizSessionCommand(Guid.NewGuid(), 10)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoQuestionCount_Passes()
    {
        _validator.Validate(new StartQuizSessionCommand(Guid.NewGuid(), null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsLessonIdRequired()
    {
        Codes(new StartQuizSessionCommand(Guid.Empty, 10)).Should().Contain(ErrorCodes.LessonIdRequired);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(21)]
    public void Validate_QuestionCountOutOfRange_FailsSessionQuestionCountInvalid(int questionCount)
    {
        Codes(new StartQuizSessionCommand(Guid.NewGuid(), questionCount)).Should().Contain(ErrorCodes.SessionQuestionCountInvalid);
    }

    private List<string> Codes(StartQuizSessionCommand command)
    {
        return _validator.Validate(command).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
