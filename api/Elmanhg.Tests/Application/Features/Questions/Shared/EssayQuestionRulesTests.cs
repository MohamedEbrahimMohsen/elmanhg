using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RichText;
using FluentAssertions;
using NSubstitute;
using System.Text.Json.Nodes;
using static Elmanhg.Tests.Builders.QuestionBuilder;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class EssayQuestionRulesTests
{
    private const string Body = """{"maxWords":200}""";
    private const string Levels = """[{"points":0,"description":"Missing"},{"points":2,"description":"Complete"}]""";
    private const string Answers = """["<p>Inertia.</p>"]""";
    private readonly ContentOptions _options = new() { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100 };

    [Fact]
    public void Validate_ValidEssay_ReturnsNoErrors()
    {
        EssayQuestionRules.Validate(Json(Body), Json(EssaySpecJson), _options).Should().BeEmpty();
    }

    [Fact]
    public void Validate_NoWordLimit_ReturnsNoErrors()
    {
        EssayQuestionRules.Validate(Json("{}"), Json(EssaySpecJson), _options).Should().BeEmpty();
    }

    [Fact]
    public void Validate_BodyNotObject_ReturnsQuestionBodyInvalid()
    {
        EssayQuestionRules.Validate(Json("[]"), Json(EssaySpecJson), _options).Should().Contain(ErrorCodes.QuestionBodyInvalid);
    }

    [Fact]
    public void Validate_SpecNotObject_ReturnsQuestionGradingSpecInvalid()
    {
        EssayQuestionRules.Validate(Json(Body), Json("\"x\""), _options).Should().Contain(ErrorCodes.QuestionGradingSpecInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2001)]
    public void Validate_MaxWordsOutOfRange_ReturnsQuestionEssayMaxWordsInvalid(int maxWords)
    {
        EssayQuestionRules.Validate(Json($$"""{"maxWords":{{maxWords}}}"""), Json(EssaySpecJson), _options).Should().Contain(ErrorCodes.QuestionEssayMaxWordsInvalid);
    }

    [Fact]
    public void Validate_NoCriteria_ReturnsQuestionRubricCriteriaCountInvalid()
    {
        Errors(Spec("[]")).Should().Contain(ErrorCodes.QuestionRubricCriteriaCountInvalid);
    }

    [Fact]
    public void Validate_TooManyCriteria_ReturnsQuestionRubricCriteriaCountInvalid()
    {
        _options.QuestionRubricCriteriaMaxCount = 1;

        Errors(Spec($"[{Criterion()},{Criterion(id: "c2")}]")).Should().Contain(ErrorCodes.QuestionRubricCriteriaCountInvalid);
    }

    [Fact]
    public void Validate_CriterionIdInvalid_ReturnsQuestionRubricCriterionIdInvalid()
    {
        Errors(Spec($"[{Criterion(id: "C 1")}]")).Should().Contain(ErrorCodes.QuestionRubricCriterionIdInvalid);
    }

    [Fact]
    public void Validate_CriterionIdDuplicate_ReturnsQuestionRubricCriterionIdDuplicate()
    {
        Errors(Spec($"[{Criterion()},{Criterion()}]")).Should().Contain(ErrorCodes.QuestionRubricCriterionIdDuplicate);
    }

    [Fact]
    public void Validate_BlankTitle_ReturnsQuestionRubricCriterionTitleRequired()
    {
        Errors(Spec($"[{Criterion(title: "  ")}]")).Should().Contain(ErrorCodes.QuestionRubricCriterionTitleRequired);
    }

    [Fact]
    public void Validate_TitleTooLong_ReturnsQuestionRubricTextTooLong()
    {
        _options.QuestionRubricTextMaxLength = 3;

        Errors(Spec($"[{Criterion(title: "Long", levels: """[{"points":0,"description":"No"},{"points":2,"description":"Yes"}]""")}]")).Should().Contain(ErrorCodes.QuestionRubricTextTooLong);
    }

    [Fact]
    public void Validate_LevelDescriptionTooLong_ReturnsQuestionRubricTextTooLong()
    {
        _options.QuestionRubricTextMaxLength = 3;

        Errors(Spec($"[{Criterion(title: "Def", levels: """[{"points":0,"description":"No"},{"points":2,"description":"Complete"}]""")}]")).Should().Contain(ErrorCodes.QuestionRubricTextTooLong);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_PointsOutOfRange_ReturnsQuestionRubricPointsInvalid(int points)
    {
        var levels = $$"""[{"points":0,"description":"No"},{"points":{{points}},"description":"Yes"}]""";

        Errors(Spec($"[{Criterion(points: points, levels: levels)}]")).Should().Contain(ErrorCodes.QuestionRubricPointsInvalid);
    }

    [Fact]
    public void Validate_FractionalPoints_ReturnsQuestionGradingSpecInvalid()
    {
        Errors(Spec($"[{Criterion().Replace("\"points\":2,\"levels\"", "\"points\":1.5,\"levels\"", StringComparison.Ordinal)}]")).Should().Contain(ErrorCodes.QuestionGradingSpecInvalid);
    }

    [Fact]
    public void Validate_OneLevel_ReturnsQuestionRubricLevelsCountInvalid()
    {
        Errors(Spec($"[{Criterion(levels: """[{"points":0,"description":"No"}]""")}]")).Should().Contain(ErrorCodes.QuestionRubricLevelsCountInvalid);
    }

    [Fact]
    public void Validate_TooManyLevels_ReturnsQuestionRubricLevelsCountInvalid()
    {
        _options.QuestionRubricLevelsMaxCount = 2;

        Errors(Spec($"[{Criterion(levels: """[{"points":0,"description":"No"},{"points":1,"description":"Half"},{"points":2,"description":"Yes"}]""")}]")).Should().Contain(ErrorCodes.QuestionRubricLevelsCountInvalid);
    }

    [Fact]
    public void Validate_BlankLevelDescription_ReturnsQuestionRubricLevelDescriptionRequired()
    {
        Errors(Spec($"[{Criterion(levels: """[{"points":0,"description":" "},{"points":2,"description":"Yes"}]""")}]")).Should().Contain(ErrorCodes.QuestionRubricLevelDescriptionRequired);
    }

    [Theory]
    [InlineData("1,4")]
    [InlineData("0,2")]
    [InlineData("0,0,4")]
    [InlineData("0,4,5")]
    [InlineData("0,null")]
    public void Validate_BrokenLevelScale_ReturnsQuestionRubricLevelPointsInvalid(string points)
    {
        var levels = "[" + string.Join(',', points.Split(',').Select(x => $$"""{"points":{{x}},"description":"Level"}""")) + "]";

        Errors(Spec($"[{Criterion(points: 4, levels: levels)}]")).Should().Contain(ErrorCodes.QuestionRubricLevelPointsInvalid);
    }

    [Fact]
    public void Validate_NoModelAnswers_ReturnsQuestionModelAnswersCountInvalid()
    {
        Errors(Spec($"[{Criterion()}]", "[]")).Should().Contain(ErrorCodes.QuestionModelAnswersCountInvalid);
    }

    [Fact]
    public void Validate_TooManyModelAnswers_ReturnsQuestionModelAnswersCountInvalid()
    {
        _options.QuestionModelAnswersMaxCount = 1;

        Errors(Spec($"[{Criterion()}]", """["<p>a</p>","<p>b</p>"]""")).Should().Contain(ErrorCodes.QuestionModelAnswersCountInvalid);
    }

    [Fact]
    public void Validate_BlankModelAnswer_ReturnsQuestionModelAnswerRequired()
    {
        Errors(Spec($"[{Criterion()}]", """["  "]""")).Should().Contain(ErrorCodes.QuestionModelAnswerRequired);
    }

    [Fact]
    public void Validate_ModelAnswerTooLong_ReturnsQuestionModelAnswerTooLong()
    {
        _options.QuestionModelAnswerMaxLength = 5;

        Errors(Spec($"[{Criterion()}]", """["<p>long</p>"]""")).Should().Contain(ErrorCodes.QuestionModelAnswerTooLong);
    }

    [Fact]
    public void Normalize_Essay_TrimsSortsLevelsOmitsBlankDescriptionAndSanitises()
    {
        var spec = """{"criteria":[{"id":"c1","title":"  Definition ","description":"  ","points":2,"extra":1,"levels":[{"points":2,"description":"Complete"},{"points":0,"description":"Missing"},{"points":1,"description":" Partial "}]}],"modelAnswers":["<p>Inertia is resistance to change in motion.</p>"],"extra":1}""";
        var expected = EssaySpecJson.Replace("<p>Inertia", "clean:<p>Inertia", StringComparison.Ordinal);

        var (body, gradingSpec) = EssayQuestionRules.Normalize(Json("""{"maxWords":200,"extra":1}"""), Json(spec), Sanitizer());

        JsonNode.DeepEquals(JsonNode.Parse(gradingSpec), JsonNode.Parse(expected)).Should().BeTrue();
        body.Should().Be(Body);
    }

    [Fact]
    public void Normalize_NoWordLimit_WritesEmptyBody()
    {
        var (body, _) = EssayQuestionRules.Normalize(Json("""{"maxWords":null}"""), Json(EssaySpecJson), Sanitizer());

        body.Should().Be("{}");
    }

    [Theory]
    [InlineData("<p></p>")]
    [InlineData("<p>&nbsp;</p>")]
    [InlineData("<p> <br></p>")]
    public void Validate_EmptyRichTextModelAnswer_ReturnsQuestionModelAnswerRequired(string answer)
    {
        Errors(Spec($"[{Criterion()}]", $"[\"{answer}\"]")).Should().Contain(ErrorCodes.QuestionModelAnswerRequired);
    }

    [Fact]
    public void Validate_ImageOnlyModelAnswerWithAlt_ReturnsNoErrors()
    {
        Errors(Spec($"[{Criterion()}]", """["<p><img src=\"/api/media/a.png\" alt=\"Graph\"></p>"]""")).Should().BeEmpty();
    }

    [Fact]
    public void Validate_ImageOnlyModelAnswerWithoutAlt_ReturnsQuestionModelAnswerRequired()
    {
        Errors(Spec($"[{Criterion()}]", """["<p><img src=\"/api/media/a.png\" alt=\"\"></p>"]""")).Should().Contain(ErrorCodes.QuestionModelAnswerRequired);
    }

    [Fact]
    public void Normalize_ModelAnswerImageWithoutAltAfterSanitising_ThrowsQuestionModelAnswerRequired()
    {
        var sanitizer = Substitute.For<IRichTextSanitizer>();
        sanitizer.Sanitize(Arg.Any<string?>()).Returns("""<p><img src="/api/media/a.png" alt=""></p>""");

        var act = () => EssayQuestionRules.Normalize(Json(Body), Json(EssaySpecJson), sanitizer);

        act.Should().Throw<ApplicationValidationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionModelAnswerRequired);
    }

    [Fact]
    public void Normalize_ModelAnswerEmptyAfterSanitising_ThrowsQuestionModelAnswerRequired()
    {
        var sanitizer = Substitute.For<IRichTextSanitizer>();
        sanitizer.Sanitize(Arg.Any<string?>()).Returns("<p></p>");

        var act = () => EssayQuestionRules.Normalize(Json(Body), Json(EssaySpecJson), sanitizer);

        act.Should().Throw<ApplicationValidationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.QuestionModelAnswerRequired);
    }

    private static string Criterion(string id = "c1", string title = "Def", int points = 2, string levels = Levels)
    {
        return $$"""{"id":"{{id}}","title":"{{title}}","points":{{points}},"levels":{{levels}}}""";
    }

    private static string Spec(string criteria, string answers = Answers)
    {
        return $$"""{"criteria":{{criteria}},"modelAnswers":{{answers}}}""";
    }

    private List<string> Errors(string spec)
    {
        return EssayQuestionRules.Validate(Json(Body), Json(spec), _options);
    }

    private static IRichTextSanitizer Sanitizer()
    {
        var sanitizer = Substitute.For<IRichTextSanitizer>();
        sanitizer.Sanitize(Arg.Any<string?>()).Returns(x => $"clean:{x.Arg<string?>()}");
        return sanitizer;
    }
}
