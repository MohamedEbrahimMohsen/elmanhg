using Core.Errors;
using Core.Spreadsheets;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;

namespace Elmanhg.Tests.Application.Features.Questions.Shared.Import;

public sealed class QuestionImportParserTests
{
    private static readonly string[] McqHeaders = ["stem", "option_a", "option_b", "correct", "difficulty"];
    private readonly QuestionBuilder _builder = new();
    private readonly ISpreadsheetReader _reader = Substitute.For<ISpreadsheetReader>();
    private readonly ContentOptions _options = ImportOptions();

    internal static ContentOptions ImportOptions() => new() { SubjectNameMaxLength = 100, UnitNameMaxLength = 100, LessonNameMaxLength = 100, LessonExplanationMaxLength = 100000, LessonSummaryMaxLength = 20000, LessonObjectiveMaxLength = 300, LessonObjectivesMaxCount = 20, LessonVideoUrlMaxLength = 2048, LessonImageMaxSizeInMb = 5, QuestionStemMaxLength = 20000, QuestionExplanationMaxLength = 20000, QuestionOptionsMaxCount = 10, QuestionOptionTextMaxLength = 2000, QuestionBlanksMaxCount = 10, QuestionAcceptedAnswersMaxCount = 20, QuestionAnswerMaxLength = 200, QuestionTagsMaxCount = 10, QuestionTagMaxLength = 50, QuestionMaxScoreMax = 100, QuestionListMaxPageSize = 100, QuestionFilterMaxLength = 200, QuestionImportMaxRows = 500, QuestionImportMaxFileSizeInMb = 5 };

    internal static SpreadsheetSheet Sheet(string name, string[] headers, params string[][] rows) => new(name, headers, rows.Select((cells, index) => new SpreadsheetRow(index + 2, cells)).ToList());

    [Fact]
    public async Task ParseAsync_ValidMcqRow_ReturnsFieldsWithLetterOptionsAndCorrectId()
    {
        var parse = await ParseAsync(Sheet("Mcq", McqHeaders, ["2 + 2 = ?", "3", "4", "b", "medium"]));

        parse.Errors.Should().BeEmpty();
        var fields = parse.Rows.Single().Fields;
        fields.Stem.Should().Be("<p>2 + 2 = ?</p>");
        fields.Body.GetProperty("options").EnumerateArray().Select(x => (x.GetProperty("id").GetString(), x.GetProperty("text").GetString())).Should().Equal(("a", "<p>3</p>"), ("b", "<p>4</p>"));
        fields.GradingSpec.GetProperty("correctOptionId").GetString().Should().Be("b");
    }

    [Fact]
    public async Task ParseAsync_ValidMultiRow_ReturnsCorrectIdsAndPartialCredit()
    {
        var parse = await ParseAsync(Sheet("Multi", ["stem", "option_a", "option_b", "option_c", "correct", "partial_credit", "difficulty"], ["Pick vectors", "Force", "Mass", "Velocity", "a|C", "true", "easy"]));

        var spec = parse.Rows.Single().Fields.GradingSpec;
        spec.GetProperty("correctOptionIds").EnumerateArray().Select(x => x.GetString()).Should().Equal("a", "c");
        spec.GetProperty("partialCredit").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_ValidTrueFalseRow_ReturnsCorrectAnswer()
    {
        var parse = await ParseAsync(Sheet("TrueFalse", ["stem", "correct_answer", "difficulty"], ["Mass is a vector.", "false", "easy"]));

        parse.Rows.Single().Fields.GradingSpec.GetProperty("correctAnswer").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_ValidFillRow_ReturnsBlanksFromFilledColumns()
    {
        var parse = await ParseAsync(Sheet("Fill", ["stem", "blank_1", "blank_2", "difficulty"], ["v = [[1]] m/s", "20|٢٠", "", "easy"]));

        var fields = parse.Rows.Single().Fields;
        fields.Body.GetProperty("blanks").EnumerateArray().Select(x => x.GetProperty("id").GetString()).Should().Equal("1");
        var answers = fields.GradingSpec.GetProperty("blanks")[0];
        answers.GetProperty("acceptedAnswers").EnumerateArray().Select(x => x.GetString()).Should().Equal("20", "٢٠");
        fields.GradingSpec.GetProperty("normalization").GetProperty("unifyAlef").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ParseAsync_NumericShortRowWithoutTolerance_DefaultsToZeroAbsolute()
    {
        var parse = await ParseAsync(Sheet("Short", ["stem", "answer_kind", "value", "difficulty"], ["g = ?", "numeric", "9.8", "medium"]));

        var spec = parse.Rows.Single().Fields.GradingSpec;
        spec.GetProperty("tolerance").GetDecimal().Should().Be(0);
        spec.GetProperty("toleranceMode").GetString().Should().Be("absolute");
        spec.GetProperty("value").GetDecimal().Should().Be(9.8m);
    }

    [Fact]
    public async Task ParseAsync_NumericShortRowWithPercentTolerance_KeepsPercentMode()
    {
        var parse = await ParseAsync(Sheet("Short", ["stem", "answer_kind", "value", "tolerance", "tolerance_mode", "difficulty"], ["g = ?", "numeric", "-200", "5", "Percent", "medium"]));

        parse.Errors.Should().BeEmpty();
        var spec = parse.Rows.Single().Fields.GradingSpec;
        spec.GetProperty("value").GetDecimal().Should().Be(-200m);
        spec.GetProperty("tolerance").GetDecimal().Should().Be(5m);
        spec.GetProperty("toleranceMode").GetString().Should().Be("percent");
    }

    [Fact]
    public async Task ParseAsync_TextShortRow_ReturnsAcceptedAnswers()
    {
        var parse = await ParseAsync(Sheet("Short", ["stem", "answer_kind", "accepted_answers", "difficulty"], ["H2O is?", "text", "ماء", "easy"]));

        parse.Rows.Single().Fields.GradingSpec.GetProperty("acceptedAnswers").EnumerateArray().Select(x => x.GetString()).Should().Equal("ماء");
    }

    [Fact]
    public async Task ParseAsync_FillRowUnifyFalse_TurnsOffOnlyLetterRules()
    {
        var parse = await ParseAsync(Sheet("Fill", ["stem", "blank_1", "unify_letter_variants", "difficulty"], ["v = [[1]] m/s", "20", "false", "easy"]));

        AssertOnlyLetterRulesOff(parse.Rows.Single().Fields.GradingSpec.GetProperty("normalization"));
    }

    [Fact]
    public async Task ParseAsync_TextShortRowUnifyFalse_TurnsOffOnlyLetterRules()
    {
        var parse = await ParseAsync(Sheet("Short", ["stem", "answer_kind", "accepted_answers", "unify_letter_variants", "difficulty"], ["H2O is?", "text", "ماء", "false", "easy"]));

        AssertOnlyLetterRulesOff(parse.Rows.Single().Fields.GradingSpec.GetProperty("normalization"));
    }

    [Fact]
    public async Task ParseAsync_BlankMaxScore_DefaultsToOne()
    {
        var parse = await ParseAsync(Sheet("Mcq", [.. McqHeaders, "max_score"], ["2 + 2 = ?", "3", "4", "b", "medium", ""]));

        parse.Rows.Single().Fields.MaxScore.Should().Be(1);
    }

    [Fact]
    public async Task ParseAsync_ObjectiveNumber_ResolvesLessonObjectiveId()
    {
        var parse = await ParseAsync(Sheet("Mcq", [.. McqHeaders, "objective"], ["2 + 2 = ?", "3", "4", "b", "medium", "1"]));

        parse.Rows.Single().Fields.ObjectiveId.Should().Be(_builder.ObjectiveId);
    }

    [Fact]
    public async Task ParseAsync_ObjectiveOutOfRange_ReportsObjectiveInvalid()
    {
        var parse = await ParseAsync(Sheet("Mcq", [.. McqHeaders, "objective"], ["2 + 2 = ?", "3", "4", "b", "medium", "2"]));

        parse.Errors.Should().Equal(new QuestionImportRowError("Mcq", 2, "objective", ErrorCodes.QuestionImportObjectiveInvalid));
        parse.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_InvalidCell_ReportsCellInvalidAndSkipsValidator()
    {
        var parse = await ParseAsync(Sheet("Mcq", McqHeaders, ["", "3", "4", "b", "very hard"]));

        parse.Errors.Should().Equal(new QuestionImportRowError("Mcq", 2, "difficulty", ErrorCodes.QuestionImportCellInvalid));
    }

    [Fact]
    public async Task ParseAsync_RuleViolation_ReportsValidatorCodeWithSheetAndRow()
    {
        var parse = await ParseAsync(Sheet("Mcq", McqHeaders, ["2 + 2 = ?", "3", "4", "z", "medium"]));

        parse.Errors.Should().Equal(new QuestionImportRowError("Mcq", 2, null, ErrorCodes.QuestionCorrectOptionInvalid));
    }

    [Fact]
    public async Task ParseAsync_MissingStem_ReportsStemRequired()
    {
        var parse = await ParseAsync(Sheet("Mcq", McqHeaders, ["", "3", "4", "b", "medium"]));

        parse.Errors.Select(x => x.Code).Should().Contain(ErrorCodes.QuestionStemRequired);
    }

    [Fact]
    public async Task ParseAsync_UnknownHeader_ReportsColumnUnknownOnRowOne()
    {
        var parse = await ParseAsync(Sheet("Mcq", ["stm", "option_a", "option_b", "correct", "difficulty"], ["2 + 2 = ?", "3", "4", "b", "medium"]));

        parse.Errors.Should().Contain(new QuestionImportRowError("Mcq", 1, "stm", ErrorCodes.QuestionImportColumnUnknown));
    }

    [Fact]
    public async Task ParseAsync_DuplicateHeader_ReportsColumnDuplicate()
    {
        var parse = await ParseAsync(Sheet("Mcq", [.. McqHeaders, "stem"], ["2 + 2 = ?", "3", "4", "b", "medium", "again"]));

        parse.Errors.Should().Contain(new QuestionImportRowError("Mcq", 1, "stem", ErrorCodes.QuestionImportColumnDuplicate));
    }

    [Fact]
    public async Task ParseAsync_UnrecognisedSheet_IsIgnored()
    {
        var parse = await ParseAsync(Sheet("Instructions", ["sheet", "column"], ["Mcq", "stem"], ["Mcq", "correct"]), Sheet("TrueFalse", ["stem", "correct_answer", "difficulty"], ["Mass is a vector.", "false", "easy"]));

        parse.TotalRows.Should().Be(1);
    }

    [Fact]
    public async Task ParseAsync_NoDataRows_ThrowsQuestionImportEmpty()
    {
        var act = () => ParseAsync(Sheet("Mcq", McqHeaders));

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionImportEmpty);
    }

    [Fact]
    public async Task ParseAsync_TooManyRows_ThrowsQuestionImportTooManyRows()
    {
        _options.QuestionImportMaxRows = 1;

        var act = () => ParseAsync(Sheet("Mcq", McqHeaders, ["one", "3", "4", "b", "easy"], ["two", "3", "4", "b", "easy"]));

        var exception = (await act.Should().ThrowAsync<BadRequestCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.QuestionImportTooManyRows);
        exception.Context!["max"].Should().Be(1);
    }

    [Fact]
    public async Task ParseAsync_SameCodeTwiceInRow_ReportedOnce()
    {
        var tooLong = new string('t', 51);

        var parse = await ParseAsync(Sheet("Mcq", [.. McqHeaders, "tags"], ["2 + 2 = ?", "3", "4", "b", "medium", $"{tooLong}|{tooLong}x"]));

        parse.Errors.Where(x => x.Code == ErrorCodes.QuestionTagTooLong).Should().ContainSingle().Which.Row.Should().Be(2);
    }

    private static void AssertOnlyLetterRulesOff(JsonElement normalization)
    {
        normalization.GetProperty("unifyAlef").GetBoolean().Should().BeFalse();
        normalization.GetProperty("unifyTaaMarbuta").GetBoolean().Should().BeFalse();
        normalization.GetProperty("unifyAlefMaqsura").GetBoolean().Should().BeFalse();
        normalization.GetProperty("stripTashkeel").GetBoolean().Should().BeTrue();
        normalization.GetProperty("foldCase").GetBoolean().Should().BeTrue();
    }

    private Task<QuestionImportParse> ParseAsync(params SpreadsheetSheet[] sheets)
    {
        _reader.Read(Arg.Any<Stream>(), Arg.Any<SpreadsheetReadLimits>()).Returns(new SpreadsheetWorkbook(sheets));
        return QuestionImportParser.ParseAsync([], _builder.Lesson, _reader, new QuestionFieldsValidator(Options.Create(_options)), _options, TestContext.Current.CancellationToken);
    }
}
