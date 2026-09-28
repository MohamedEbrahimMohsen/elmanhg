using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class FillQuestionRulesTests
{
    private const string Stem = "<p>v = [[1]] m/s</p>";
    private const string OneBlank = """{"blanks":[{"id":"1"}]}""";
    private const string OneAnswer = """{"blanks":[{"id":"1","acceptedAnswers":["20"]}]}""";
    private readonly ContentOptions _options = new() { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100 };

    [Fact]
    public void Validate_ValidFill_ReturnsNoErrors()
    {
        FillQuestionRules.Validate(Stem, Json(OneBlank), Json(OneAnswer), _options).Should().BeEmpty();
    }

    [Fact]
    public void Validate_NoBlanks_ReturnsQuestionBlanksCountInvalid()
    {
        FillQuestionRules.Validate(Stem, Json("""{"blanks":[]}"""), Json("""{"blanks":[]}"""), _options).Should().Contain(ErrorCodes.QuestionBlanksCountInvalid);
    }

    [Fact]
    public void Validate_MoreBlanksThanCap_ReturnsQuestionBlanksCountInvalid()
    {
        _options.QuestionBlanksMaxCount = 1;

        var errors = FillQuestionRules.Validate("[[1]] [[2]]", Json("""{"blanks":[{"id":"1"},{"id":"2"}]}"""), Json("""{"blanks":[{"id":"1","acceptedAnswers":["a"]},{"id":"2","acceptedAnswers":["b"]}]}"""), _options);

        errors.Should().Contain(ErrorCodes.QuestionBlanksCountInvalid);
    }

    [Fact]
    public void Validate_BlankIdWithSpace_ReturnsQuestionBlankIdInvalid()
    {
        FillQuestionRules.Validate("[[a b]]", Json("""{"blanks":[{"id":"a b"}]}"""), Json("""{"blanks":[{"id":"a b","acceptedAnswers":["x"]}]}"""), _options).Should().Contain(ErrorCodes.QuestionBlankIdInvalid);
    }

    [Fact]
    public void Validate_DuplicateBlankIds_ReturnsQuestionBlankIdDuplicate()
    {
        FillQuestionRules.Validate(Stem, Json("""{"blanks":[{"id":"1"},{"id":"1"}]}"""), Json(OneAnswer), _options).Should().Contain(ErrorCodes.QuestionBlankIdDuplicate);
    }

    [Fact]
    public void Validate_PlaceholderMissingFromStem_ReturnsQuestionBlankPlaceholderMissing()
    {
        FillQuestionRules.Validate("<p>v = ? m/s</p>", Json(OneBlank), Json(OneAnswer), _options).Should().Contain(ErrorCodes.QuestionBlankPlaceholderMissing);
    }

    [Fact]
    public void Validate_PlaceholderTwiceInStem_ReturnsQuestionBlankPlaceholderMissing()
    {
        FillQuestionRules.Validate("<p>[[1]] and [[1]]</p>", Json(OneBlank), Json(OneAnswer), _options).Should().Contain(ErrorCodes.QuestionBlankPlaceholderMissing);
    }

    [Fact]
    public void Validate_AnswersForUnknownBlank_ReturnsQuestionBlankAnswersMismatch()
    {
        FillQuestionRules.Validate(Stem, Json(OneBlank), Json("""{"blanks":[{"id":"2","acceptedAnswers":["20"]}]}"""), _options).Should().Contain(ErrorCodes.QuestionBlankAnswersMismatch);
    }

    [Fact]
    public void Validate_EmptyAcceptedAnswers_ReturnsQuestionAcceptedAnswersInvalid()
    {
        FillQuestionRules.Validate(Stem, Json(OneBlank), Json("""{"blanks":[{"id":"1","acceptedAnswers":[]}]}"""), _options).Should().Contain(ErrorCodes.QuestionAcceptedAnswersInvalid);
    }

    [Fact]
    public void Validate_AcceptedAnswerOverCap_ReturnsQuestionAcceptedAnswersInvalid()
    {
        _options.QuestionAnswerMaxLength = 3;

        FillQuestionRules.Validate(Stem, Json(OneBlank), Json("""{"blanks":[{"id":"1","acceptedAnswers":["twenty"]}]}"""), _options).Should().Contain(ErrorCodes.QuestionAcceptedAnswersInvalid);
    }

    [Fact]
    public void Validate_MoreAcceptedAnswersThanCap_ReturnsQuestionAcceptedAnswersInvalid()
    {
        _options.QuestionAcceptedAnswersMaxCount = 1;

        FillQuestionRules.Validate(Stem, Json(OneBlank), Json("""{"blanks":[{"id":"1","acceptedAnswers":["20","twenty"]}]}"""), _options).Should().Contain(ErrorCodes.QuestionAcceptedAnswersInvalid);
    }

    [Fact]
    public void Normalize_MissingUnifyFlag_DefaultsTrueTrimsAndFollowsBodyOrder()
    {
        var body = """{"blanks":[{"id":"b"},{"id":"a"}]}""";
        var spec = """{"blanks":[{"id":"a","acceptedAnswers":[" x "]},{"id":"b","acceptedAnswers":["y "]}]}""";

        var (_, normalizedSpec) = FillQuestionRules.Normalize(Json(body), Json(spec));

        normalizedSpec.Should().Be("""{"blanks":[{"id":"b","acceptedAnswers":["y"]},{"id":"a","acceptedAnswers":["x"]}],"unifyLetterVariants":true}""");
    }
}
