using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.CreateQuestion;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Tests.Application.Features.Questions.Shared;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Questions.CreateQuestion;

public sealed class CreateQuestionValidatorTests
{
    private readonly CreateQuestionValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100 }));

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        _validator.Validate(new CreateQuestionCommand(Guid.NewGuid(), QuestionFieldsValidatorTests.ValidMcq())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_HasLessonIdRequired()
    {
        var result = _validator.Validate(new CreateQuestionCommand(Guid.Empty, QuestionFieldsValidatorTests.ValidMcq()));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }

    [Fact]
    public void Validate_InvalidFields_SurfacesFieldCode()
    {
        var result = _validator.Validate(new CreateQuestionCommand(Guid.NewGuid(), QuestionFieldsValidatorTests.ValidMcq() with { Stem = string.Empty }));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.QuestionStemRequired);
    }
}
