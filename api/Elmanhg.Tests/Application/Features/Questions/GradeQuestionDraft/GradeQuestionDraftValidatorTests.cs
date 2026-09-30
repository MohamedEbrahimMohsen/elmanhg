using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.GradeQuestionDraft;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.GradeQuestionDraft;

public sealed class GradeQuestionDraftValidatorTests
{
    private readonly GradeQuestionDraftValidator _validator = new(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100, QuestionListMaxPageSize = 100, QuestionFilterMaxLength = 200 }));

    [Fact]
    public void Validate_ValidDraftAndAnswer_HasNoErrors()
    {
        _validator.Validate(new GradeQuestionDraftQuery(McqFields(), Json("""{"optionId":"b"}"""))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidDraft_SurfacesSchemaCode()
    {
        var draft = McqFields() with { GradingSpec = Json("""{"correctOptionId":"z"}""") };

        Codes(new GradeQuestionDraftQuery(draft, Json("""{"optionId":"b"}"""))).Should().Contain(ErrorCodes.QuestionCorrectOptionInvalid);
    }

    [Fact]
    public void Validate_AnswerNotAnObject_HasQuestionAnswerInvalid()
    {
        Codes(new GradeQuestionDraftQuery(McqFields(), Json("\"b\""))).Should().Contain(ErrorCodes.QuestionAnswerInvalid);
    }

    [Fact]
    public void Validate_AnswerWrongShape_HasQuestionAnswerInvalid()
    {
        Codes(new GradeQuestionDraftQuery(McqFields(), Json("""{"optionId":5}"""))).Should().Contain(ErrorCodes.QuestionAnswerInvalid);
    }

    [Fact]
    public void Validate_EmptyAnswerObject_HasNoErrors()
    {
        _validator.Validate(new GradeQuestionDraftQuery(McqFields(), Json("{}"))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingType_SkipsAnswerRule()
    {
        var codes = Codes(new GradeQuestionDraftQuery(McqFields() with { Type = null }, Json("\"b\"")));

        codes.Should().Contain(ErrorCodes.QuestionTypeRequired).And.NotContain(ErrorCodes.QuestionAnswerInvalid);
    }

    [Fact]
    public void Validate_EssayWithText_HasNoErrors()
    {
        _validator.Validate(new GradeQuestionDraftQuery(EssayFields(), Json("""{"text":"Inertia"}"""))).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EssayWithoutText_HasQuestionAnswerInvalid()
    {
        Codes(new GradeQuestionDraftQuery(EssayFields(), Json("{}"))).Should().Contain(ErrorCodes.QuestionAnswerInvalid);
    }

    [Fact]
    public void Validate_EssayTooLong_HasQuestionEssayAnswerTooLong()
    {
        var validator = new GradeQuestionDraftValidator(Options.Create(new ContentOptions { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100, QuestionListMaxPageSize = 100, QuestionFilterMaxLength = 200, QuestionEssayAnswerMaxLength = 5 }));

        var codes = validator.Validate(new GradeQuestionDraftQuery(EssayFields(), Json("""{"text":"abcdef"}"""))).Errors
            .Select(x => x.ErrorCode)
            .ToList();

        codes.Should().Equal(ErrorCodes.QuestionEssayAnswerTooLong);
    }

    [Fact]
    public void Validate_DragDrop_ReturnsQuestionTypeNotGradableOnly()
    {
        var codes = Codes(new GradeQuestionDraftQuery(DragDropFields(), Json("{}")));

        codes.Should().Contain(ErrorCodes.QuestionTypeNotGradable).And.NotContain(ErrorCodes.QuestionAnswerInvalid);
    }

    private List<string> Codes(GradeQuestionDraftQuery query)
    {
        return _validator.Validate(query).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
