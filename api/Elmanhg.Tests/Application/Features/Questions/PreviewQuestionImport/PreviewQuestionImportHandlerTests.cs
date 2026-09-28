using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.PreviewQuestionImport;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Application.Shared.Spreadsheets;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using static Elmanhg.Tests.Application.Features.Questions.Shared.Import.QuestionImportParserTests;

namespace Elmanhg.Tests.Application.Features.Questions.PreviewQuestionImport;

public sealed class PreviewQuestionImportHandlerTests
{
    private static readonly string[] TrueFalseHeaders = ["stem", "correct_answer", "difficulty"];
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ISpreadsheetReader _reader = Substitute.For<ISpreadsheetReader>();
    private readonly QuestionBuilder _builder = new();
    private readonly PreviewQuestionImportHandler _handler;

    public PreviewQuestionImportHandlerTests()
    {
        var options = Options.Create(ImportOptions());
        _lessonRepository.GetWithObjectivesAsync(_builder.Lesson.Id, true, Arg.Any<CancellationToken>()).Returns(_builder.Lesson);
        _handler = new PreviewQuestionImportHandler(_lessonRepository, _reader, new QuestionFieldsValidator(options), options);
    }

    [Fact]
    public async Task Handle_UnknownLesson_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new PreviewQuestionImportQuery(Guid.NewGuid(), PreviewQuestionImportValidatorTests.Upload("q.xlsx", 10)), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        _reader.DidNotReceive().Read(Arg.Any<Stream>(), Arg.Any<SpreadsheetReadLimits>());
    }

    [Fact]
    public async Task Handle_ValidWorkbook_ReturnsCountsPerTypeAndNoErrors()
    {
        _reader.Read(Arg.Any<Stream>(), Arg.Any<SpreadsheetReadLimits>()).Returns(new SpreadsheetWorkbook([Sheet("TrueFalse", TrueFalseHeaders, ["Mass is a vector.", "false", "easy"], ["Force is a vector.", "true", "easy"]), Sheet("Mcq", ["stem", "option_a", "option_b", "correct", "difficulty"], ["2 + 2 = ?", "3", "4", "b", "medium"])]));

        var result = await _handler.Handle(new PreviewQuestionImportQuery(_builder.Lesson.Id, PreviewQuestionImportValidatorTests.Upload("q.xlsx", 10)), TestContext.Current.CancellationToken);

        (result.TotalRows, result.ValidRows).Should().Be((3, 3));
        result.Types.Should().Equal(new QuestionImportTypeCount(QuestionType.Mcq, 1), new QuestionImportTypeCount(QuestionType.TrueFalse, 2));
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RowErrors_ReturnsErrorsAndValidCount()
    {
        _reader.Read(Arg.Any<Stream>(), Arg.Any<SpreadsheetReadLimits>()).Returns(new SpreadsheetWorkbook([Sheet("TrueFalse", TrueFalseHeaders, ["Mass is a vector.", "false", "easy"], ["Force is a vector.", "perhaps", "easy"])]));

        var result = await _handler.Handle(new PreviewQuestionImportQuery(_builder.Lesson.Id, PreviewQuestionImportValidatorTests.Upload("q.xlsx", 10)), TestContext.Current.CancellationToken);

        result.ValidRows.Should().Be(1);
        result.Errors.Should().Equal(new QuestionImportRowError("TrueFalse", 3, "correct_answer", ErrorCodes.QuestionImportCellInvalid));
    }
}
