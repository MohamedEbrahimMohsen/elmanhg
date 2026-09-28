using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.ImportQuestions;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using MediatR;
using NSubstitute;
using static Elmanhg.Tests.Application.Features.Questions.ImportQuestions.ImportQuestionsHandlerTests;

namespace Elmanhg.Tests.Application.Features.Questions.ImportQuestions;

public sealed class ImportQuestionsReplayBehaviourTests
{
    private readonly IQuestionImportBatchRepository _importBatchRepository = Substitute.For<IQuestionImportBatchRepository>();
    private readonly Guid _lessonId = Guid.NewGuid();
    private readonly Guid _batchId = Guid.NewGuid();
    private readonly ImportQuestionsReplayBehaviour _behaviour;

    public ImportQuestionsReplayBehaviourTests()
    {
        _behaviour = new ImportQuestionsReplayBehaviour(_importBatchRepository);
    }

    [Fact]
    public async Task Handle_ConcurrentBatchSameFile_ReturnsReplay()
    {
        StoreBatch(ContentHash);

        var result = await _behaviour.Handle(Command(), RacedSave, TestContext.Current.CancellationToken);

        result.Should().Be(new ImportQuestionsResult(_batchId, 4, true));
    }

    [Fact]
    public async Task Handle_ConcurrentBatchDifferentFile_ThrowsBatchConflict()
    {
        StoreBatch("other-hash");

        var act = () => _behaviour.Handle(Command(), RacedSave, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuestionImportBatchConflict);
    }

    [Fact]
    public async Task Handle_ConflictWithoutStoredBatch_Rethrows()
    {
        var act = () => _behaviour.Handle(Command(), RacedSave, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.InnerException.Should().BeOfType<InvalidOperationException>();
    }

    private static Task<ImportQuestionsResult> RacedSave(CancellationToken cancellationToken) => throw new ConflictCoreException(ErrorCodes.QuestionImportBatchConflict, innerException: new InvalidOperationException("duplicate key"));

    private ImportQuestionsCommand Command() => new(_lessonId, _batchId, Upload());

    private void StoreBatch(string hash)
    {
        var stored = QuestionImportBatch.Create(_batchId, _lessonId, hash, 4, Guid.NewGuid());
        _importBatchRepository.GetByIdAsync(_batchId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<QuestionImportBatch>, IQueryable<QuestionImportBatch>>?>(), Arg.Any<bool>()).Returns(stored);
    }
}
