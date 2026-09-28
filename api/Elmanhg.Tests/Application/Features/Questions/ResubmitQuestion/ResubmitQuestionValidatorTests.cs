using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.ResubmitQuestion;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Questions.ResubmitQuestion;

public sealed class ResubmitQuestionValidatorTests
{
    private readonly ResubmitQuestionValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100, QuestionListMaxPageSize = 100, QuestionFilterMaxLength = 200 }));

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        _validator.Validate(new ResubmitQuestionCommand(Guid.NewGuid(), QuestionBuilder.McqFields())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyQuestionId_HasQuestionIdRequired()
    {
        var result = _validator.Validate(new ResubmitQuestionCommand(Guid.Empty, QuestionBuilder.McqFields()));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.QuestionIdRequired);
    }

    [Fact]
    public void Validate_InvalidFields_SurfacesFieldCode()
    {
        var result = _validator.Validate(new ResubmitQuestionCommand(Guid.NewGuid(), QuestionBuilder.McqFields(string.Empty)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.QuestionStemRequired);
    }
}
