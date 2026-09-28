using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using FluentAssertions;
using NSubstitute;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class ChoiceQuestionRulesTests
{
    private const string TwoOptions = """{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}""";
    private readonly ContentOptions _options = new() { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100 };

    [Fact]
    public void Validate_ValidMcq_ReturnsNoErrors()
    {
        ChoiceQuestionRules.Validate(Json(TwoOptions), Json("""{"correctOptionId":"b"}"""), multiple: false, _options).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidMulti_ReturnsNoErrors()
    {
        ChoiceQuestionRules.Validate(Json(TwoOptions), Json("""{"correctOptionIds":["a","b"],"partialCredit":true}"""), multiple: true, _options).Should().BeEmpty();
    }

    [Fact]
    public void Validate_BodyNotAnObject_ReturnsQuestionBodyInvalid()
    {
        ChoiceQuestionRules.Validate(Json("[]"), Json("""{"correctOptionId":"b"}"""), multiple: false, _options).Should().Contain(ErrorCodes.QuestionBodyInvalid);
    }

    [Fact]
    public void Validate_GradingSpecWrongShape_ReturnsQuestionGradingSpecInvalid()
    {
        ChoiceQuestionRules.Validate(Json(TwoOptions), Json("""{"correctOptionId":5}"""), multiple: false, _options).Should().Contain(ErrorCodes.QuestionGradingSpecInvalid);
    }

    [Fact]
    public void Validate_OneOption_ReturnsQuestionOptionsCountInvalid()
    {
        ChoiceQuestionRules.Validate(Json("""{"options":[{"id":"a","text":"3"}]}"""), Json("""{"correctOptionId":"a"}"""), multiple: false, _options).Should().Contain(ErrorCodes.QuestionOptionsCountInvalid);
    }

    [Fact]
    public void Validate_MoreOptionsThanCap_ReturnsQuestionOptionsCountInvalid()
    {
        _options.QuestionOptionsMaxCount = 3;
        var body = """{"options":[{"id":"a","text":"1"},{"id":"b","text":"2"},{"id":"c","text":"3"},{"id":"d","text":"4"}]}""";

        ChoiceQuestionRules.Validate(Json(body), Json("""{"correctOptionId":"a"}"""), multiple: false, _options).Should().Contain(ErrorCodes.QuestionOptionsCountInvalid);
    }

    [Fact]
    public void Validate_OptionIdWithUppercase_ReturnsQuestionOptionIdInvalid()
    {
        ChoiceQuestionRules.Validate(Json("""{"options":[{"id":"A","text":"3"},{"id":"b","text":"4"}]}"""), Json("""{"correctOptionId":"b"}"""), multiple: false, _options).Should().Contain(ErrorCodes.QuestionOptionIdInvalid);
    }

    [Fact]
    public void Validate_DuplicateOptionIds_ReturnsQuestionOptionIdDuplicate()
    {
        ChoiceQuestionRules.Validate(Json("""{"options":[{"id":"a","text":"3"},{"id":"a","text":"4"}]}"""), Json("""{"correctOptionId":"a"}"""), multiple: false, _options).Should().Contain(ErrorCodes.QuestionOptionIdDuplicate);
    }

    [Fact]
    public void Validate_BlankOptionText_ReturnsQuestionOptionTextRequired()
    {
        ChoiceQuestionRules.Validate(Json("""{"options":[{"id":"a","text":" "},{"id":"b","text":"4"}]}"""), Json("""{"correctOptionId":"b"}"""), multiple: false, _options).Should().Contain(ErrorCodes.QuestionOptionTextRequired);
    }

    [Fact]
    public void Validate_OptionTextOverCap_ReturnsQuestionOptionTextTooLong()
    {
        _options.QuestionOptionTextMaxLength = 5;

        ChoiceQuestionRules.Validate(Json("""{"options":[{"id":"a","text":"123456"},{"id":"b","text":"4"}]}"""), Json("""{"correctOptionId":"b"}"""), multiple: false, _options).Should().Contain(ErrorCodes.QuestionOptionTextTooLong);
    }

    [Fact]
    public void Validate_McqCorrectOptionUnknown_ReturnsQuestionCorrectOptionInvalid()
    {
        ChoiceQuestionRules.Validate(Json(TwoOptions), Json("""{"correctOptionId":"z"}"""), multiple: false, _options).Should().Contain(ErrorCodes.QuestionCorrectOptionInvalid);
    }

    [Fact]
    public void Validate_MultiNoCorrectOptions_ReturnsQuestionCorrectOptionInvalid()
    {
        ChoiceQuestionRules.Validate(Json(TwoOptions), Json("""{"correctOptionIds":[]}"""), multiple: true, _options).Should().Contain(ErrorCodes.QuestionCorrectOptionInvalid);
    }

    [Fact]
    public void Validate_MultiCorrectOptionUnknown_ReturnsQuestionCorrectOptionInvalid()
    {
        ChoiceQuestionRules.Validate(Json(TwoOptions), Json("""{"correctOptionIds":["a","z"]}"""), multiple: true, _options).Should().Contain(ErrorCodes.QuestionCorrectOptionInvalid);
    }

    [Fact]
    public void Normalize_Mcq_SanitisesOptionTextAndDropsUnknownProperties()
    {
        var body = """{"options":[{"id":"a","text":"3","hint":"x"},{"id":"b","text":"4"}],"shuffle":true}""";

        var (normalizedBody, normalizedSpec) = ChoiceQuestionRules.Normalize(Json(body), Json("""{"correctOptionId":"b","extra":1}"""), multiple: false, Sanitizer());

        normalizedBody.Should().Be("""{"options":[{"id":"a","text":"clean:3"},{"id":"b","text":"clean:4"}]}""");
        normalizedSpec.Should().Be("""{"correctOptionId":"b"}""");
    }

    [Fact]
    public void Normalize_Multi_DeduplicatesCorrectIdsAndDefaultsPartialCreditFalse()
    {
        var (_, normalizedSpec) = ChoiceQuestionRules.Normalize(Json(TwoOptions), Json("""{"correctOptionIds":["a","a"]}"""), multiple: true, Sanitizer());

        normalizedSpec.Should().Be("""{"correctOptionIds":["a"],"partialCredit":false}""");
    }

    private static IRichTextSanitizer Sanitizer()
    {
        var sanitizer = Substitute.For<IRichTextSanitizer>();
        sanitizer.Sanitize(Arg.Any<string?>()).Returns(x => $"clean:{x.Arg<string?>()}");
        return sanitizer;
    }
}
