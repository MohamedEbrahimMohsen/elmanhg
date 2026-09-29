using Elmanhg.Application.Avatar.SendAvatarMessage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Avatar;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Avatar.SendAvatarMessage;

public sealed class SendAvatarMessageValidatorTests
{
    private readonly SendAvatarMessageValidator _validator = new(Options.Create(new AvatarOptions()));

    [Fact]
    public void Validate_LessonEntryWithLessonId_Passes()
    {
        var result = _validator.Validate(Lesson() with { ConversationId = Guid.NewGuid() });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_GlobalEntryWithoutIds_Passes()
    {
        var result = _validator.Validate(new SendAvatarMessageCommand(AvatarEntryPoint.Global, null, null, null, null, "كيف أذاكر؟"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_UndefinedEntryPoint_FailsWithAvatarEntryPointInvalid()
    {
        var result = _validator.Validate(Lesson() with { EntryPoint = (AvatarEntryPoint)99 });

        Codes(result).Should().Contain(ErrorCodes.AvatarEntryPointInvalid);
    }

    [Fact]
    public void Validate_LessonEntryWithoutLessonId_FailsWithLessonIdRequired()
    {
        var result = _validator.Validate(Lesson() with { LessonId = null });

        Codes(result).Should().Contain(ErrorCodes.LessonIdRequired);
    }

    [Fact]
    public void Validate_QuizEntryWithoutSessionId_FailsWithSessionIdRequired()
    {
        var result = _validator.Validate(new SendAvatarMessageCommand(AvatarEntryPoint.QuizQuestion, null, null, Guid.NewGuid(), null, "لماذا؟"));

        Codes(result).Should().Contain(ErrorCodes.SessionIdRequired);
    }

    [Fact]
    public void Validate_ExamReviewEntryWithoutQuestionId_FailsWithQuestionIdRequired()
    {
        var result = _validator.Validate(new SendAvatarMessageCommand(AvatarEntryPoint.ExamReview, null, Guid.NewGuid(), null, null, "لماذا؟"));

        Codes(result).Should().Contain(ErrorCodes.QuestionIdRequired);
    }

    [Fact]
    public void Validate_BlankMessage_FailsWithAvatarMessageRequired()
    {
        var result = _validator.Validate(Lesson() with { Message = "   " });

        Codes(result).Should().Contain(ErrorCodes.AvatarMessageRequired);
    }

    [Fact]
    public void Validate_MessageOverMax_FailsWithAvatarMessageTooLong()
    {
        var result = _validator.Validate(Lesson() with { Message = new string('q', 2001) });

        Codes(result).Should().Contain(ErrorCodes.AvatarMessageTooLong);
    }

    private static SendAvatarMessageCommand Lesson() => new(AvatarEntryPoint.Lesson, Guid.NewGuid(), null, null, null, "ما هو قانون أوم؟");

    private static IEnumerable<string> Codes(ValidationResult result) => result.Errors.Select(x => x.ErrorCode);
}
