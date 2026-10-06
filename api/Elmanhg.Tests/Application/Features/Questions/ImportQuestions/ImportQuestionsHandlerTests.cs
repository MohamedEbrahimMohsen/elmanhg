using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Spreadsheets;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.ImportQuestions;
using Elmanhg.Application.Questions.Shared;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Cryptography;
using static Elmanhg.Tests.Application.Features.Questions.Shared.Import.QuestionImportParserTests;

namespace Elmanhg.Tests.Application.Features.Questions.ImportQuestions;

public sealed class ImportQuestionsHandlerTests
{
    private static readonly byte[] Content = [0x50, 0x4B, 0x03, 0x04, 0x01];
    private static readonly string[] TrueFalseHeaders = ["stem", "correct_answer", "difficulty"];
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IQuestionImportBatchRepository _importBatchRepository = Substitute.For<IQuestionImportBatchRepository>();
    private readonly ISpreadsheetReader _reader = Substitute.For<ISpreadsheetReader>();
    private readonly IRichTextSanitizer _richTextSanitizer = Substitute.For<IRichTextSanitizer>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly QuestionBuilder _builder = new();
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly ImportQuestionsHandler _handler;

    public ImportQuestionsHandlerTests()
    {
        var options = Options.Create(ImportOptions());
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _richTextSanitizer.Sanitize(Arg.Any<string?>()).Returns(x => $"clean:{x.Arg<string?>()}");
        _lessonRepository.GetWithObjectivesAsync(_builder.Lesson.Id, true, Arg.Any<CancellationToken>()).Returns(_builder.Lesson);
        _unitRepository.GetByIdAsync(_builder.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns(_builder.Unit);
        _reader.Read(Arg.Any<Stream>(), Arg.Any<SpreadsheetReadLimits>()).Returns(new SpreadsheetWorkbook([Sheet("TrueFalse", TrueFalseHeaders, ["Mass is a vector.", "false", "easy"], ["Force is a vector.", "true", "easy"])]));
        _handler = new ImportQuestionsHandler(_lessonRepository, _unitRepository, _questionRepository, _importBatchRepository, _reader, new QuestionFieldsValidator(options), _richTextSanitizer, options, _currentUserService);
    }

    internal static string ContentHash => Convert.ToHexStringLower(SHA256.HashData(Content));

    internal static FormFile Upload() => new(new MemoryStream(Content), 0, Content.Length, "file", "q.xlsx");

    [Fact]
    public async Task Handle_NoUser_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(Command(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidWorkbook_AddsBatchAndPendingQuestionsAndSavesOnce()
    {
        QuestionImportBatch? batch = null;
        List<Question>? questions = null;
        await _importBatchRepository.AddAsync(Arg.Do<QuestionImportBatch>(x => batch = x), Arg.Any<CancellationToken>());
        await _questionRepository.AddRangeAsync(Arg.Do<List<Question>>(x => questions = x), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(Command(), TestContext.Current.CancellationToken);

        (batch!.Id, batch.QuestionCount, batch.FileHash).Should().Be((_batchId, 2, ContentHash));
        questions.Should().HaveCount(2).And.OnlyContain(x => x.ValidationStatus == QuestionValidationStatus.Pending && x.ImportBatchId == _batchId);
        questions![0].Stem.Should().Be("clean:<p>Mass is a vector.</p>");
        result.Should().Be(new ImportQuestionsResult(_batchId, 2, false));
        await _questionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingBatchSameFile_ReturnsReplayWithoutSaving()
    {
        StoreBatch(_builder.Lesson.Id, ContentHash);

        var result = await _handler.Handle(Command(), TestContext.Current.CancellationToken);

        result.Should().Be(new ImportQuestionsResult(_batchId, 7, true));
        _reader.DidNotReceive().Read(Arg.Any<Stream>(), Arg.Any<SpreadsheetReadLimits>());
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingBatchDifferentFile_ThrowsBatchConflict()
    {
        StoreBatch(_builder.Lesson.Id, "other-hash");

        var act = () => _handler.Handle(Command(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionImportBatchConflict);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingBatchOtherLesson_ThrowsBatchConflict()
    {
        StoreBatch(Guid.NewGuid(), ContentHash);

        var act = () => _handler.Handle(Command(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionImportBatchConflict);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownLesson_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new ImportQuestionsCommand(Guid.NewGuid(), _batchId, Upload()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownUnit_ThrowsUnitNotFound()
    {
        _unitRepository.GetByIdAsync(_builder.Unit.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns((CurriculumUnit?)null);

        var act = () => _handler.Handle(Command(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UnitNotFound);
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RowErrors_ThrowsHasErrorsWithoutSaving()
    {
        _reader.Read(Arg.Any<Stream>(), Arg.Any<SpreadsheetReadLimits>()).Returns(new SpreadsheetWorkbook([Sheet("TrueFalse", TrueFalseHeaders, ["Mass is a vector.", "false", "easy"], ["Force is a vector.", "perhaps", "easy"])]));

        var act = () => _handler.Handle(Command(), TestContext.Current.CancellationToken);

        var exception = (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which;
        exception.ErrorCode.Should().Be(ErrorCodes.QuestionImportHasErrors);
        exception.Context!["count"].Should().Be(1);
        await _questionRepository.DidNotReceive().AddRangeAsync(Arg.Any<List<Question>>(), Arg.Any<CancellationToken>());
        await _questionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private ImportQuestionsCommand Command() => new(_builder.Lesson.Id, _batchId, Upload());

    private void StoreBatch(Guid lessonId, string hash)
    {
        var stored = QuestionImportBatch.Create(_batchId, lessonId, hash, 7, Guid.NewGuid());
        _importBatchRepository.GetByIdAsync(_batchId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<QuestionImportBatch>, IQueryable<QuestionImportBatch>>?>(), Arg.Any<bool>()).Returns(stored);
    }
}
