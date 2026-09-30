using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Application.Shared.TrainingData;
using Elmanhg.Application.TrainingExports.RunTrainingExport;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;

namespace Elmanhg.Tests.Application.Features.TrainingExports.RunTrainingExport;

public sealed class RunTrainingExportHandlerTests
{
    private const string StudentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = TrainingExportBuilder.DefaultRequestedAt.AddSeconds(30);
    private readonly ITrainingExportRepository _trainingExportRepository = Substitute.For<ITrainingExportRepository>();
    private readonly IAttemptTrainingRecordRepository _attemptRepository = Substitute.For<IAttemptTrainingRecordRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<TrainingExport> _exports = [];
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly List<string> _events = [];
    private string? _savedKey;
    private byte[] _savedBytes = [];
    private int _failingSave;
    private Exception _saveFailure = new OperationCanceledException();

    public RunTrainingExportHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _trainingExportRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TrainingExport, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TrainingExport>, IQueryable<TrainingExport>>?>(), Arg.Any<Func<IQueryable<TrainingExport>, IOrderedQueryable<TrainingExport>>?>(), Arg.Any<bool>())
            .Returns(call => _exports.FirstOrDefault(call.Arg<Expression<Func<TrainingExport, bool>>>().Compile()));
        _fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            using var copy = new MemoryStream();
            call.Arg<Stream>().CopyTo(copy);
            (_savedBytes, _savedKey) = (copy.ToArray(), call.Arg<string>());
            _events.Add("upload:" + _savedKey);
            return "/api/media/" + _savedKey;
        });
        _fileStorage.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _events.Add("delete:" + call.Arg<string>());
            return Task.CompletedTask;
        });
        _trainingExportRepository.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _events.Add("save");
            return _events.Count(x => x == "save") == _failingSave ? Task.FromException<int>(_saveFailure) : Task.FromResult(1);
        });
    }

    [Fact]
    public async Task Handle_AttemptsSource_UploadsJsonlAndCompletes()
    {
        var export = Seed();
        GivenPages(AttemptRecords(2));

        await Handle(export.Id, batchSize: 500);

        _savedKey.Should().MatchRegex($"^training-exports/{export.Id:N}-[0-9a-f]{{32}}\\.jsonl$");
        var lines = Encoding.UTF8.GetString(_savedBytes).Split('\n');
        lines.Should().HaveCount(3).And.EndWith(string.Empty);
        (export.Status, export.FileKey, export.RowCount, export.FileSizeBytes).Should().Be((TrainingExportStatus.Completed, _savedKey, (long?)2, (long?)_savedBytes.Length));
        export.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(_savedBytes)));
        await _attemptRepository.Received(1).GetExportPageAsync(new TrainingRecordFilter(export.From, export.To, _subjectId), null, 500, Arg.Any<CancellationToken>());
        _events.Should().Equal("save", "upload:" + _savedKey, "save");
    }

    [Fact]
    public async Task Handle_MultiplePages_PassesCursorUntilShortPage()
    {
        var export = Seed();
        var records = AttemptRecords(3);
        var cursor = new TrainingRecordCursor(records[1].OccurredAt, records[1].Id);
        GivenPages([records[0], records[1]], [records[2]]);

        await Handle(export.Id, batchSize: 2);

        await _attemptRepository.Received(2).GetExportPageAsync(Arg.Any<TrainingRecordFilter>(), Arg.Any<TrainingRecordCursor?>(), 2, Arg.Any<CancellationToken>());
        await _attemptRepository.Received(1).GetExportPageAsync(Arg.Any<TrainingRecordFilter>(), cursor, 2, Arg.Any<CancellationToken>());
        export.RowCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_EmptyRange_CompletesWithZeroRows()
    {
        var export = Seed();
        GivenPages(new List<AttemptTrainingRecord>());

        await Handle(export.Id, batchSize: 500);

        (export.Status, export.RowCount, export.FileSizeBytes).Should().Be((TrainingExportStatus.Completed, (long?)0, (long?)0));
        await _trainingExportRepository.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotDue_DoesNothing()
    {
        var export = Seed();
        export.FailAttempt("InvalidOperationException", Now, 3, TimeSpan.FromMinutes(1));

        await Handle(export.Id, batchSize: 500);

        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _trainingExportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownExport_DoesNothing()
    {
        await Handle(Guid.NewGuid(), batchSize: 500);

        await _fileStorage.DidNotReceive().SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _trainingExportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StorageFails_PropagatesAndLeavesPendingWithClaimedKey()
    {
        var export = Seed();
        GivenPages(AttemptRecords(1));
        _fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromException<string>(new IOException("disk")));

        var act = () => Handle(export.Id, batchSize: 500);

        await act.Should().ThrowAsync<IOException>();
        (export.Status, export.NextAttemptAt).Should().Be((TrainingExportStatus.Pending, (DateTimeOffset?)Now.AddMinutes(30)));
        export.FileKey.Should().MatchRegex($"^training-exports/{export.Id:N}-[0-9a-f]{{32}}\\.jsonl$");
        _events.Should().Equal("save");
    }

    [Fact]
    public async Task Handle_SaveFailsAfterUpload_UploadedKeyWasRecordedBeforeUploading()
    {
        var export = Seed();
        GivenPages(AttemptRecords(1));
        _failingSave = 2;

        var act = () => Handle(export.Id, batchSize: 500);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _events.Should().Equal("save", "upload:" + _savedKey, "save");
        export.FileKey.Should().Be(_savedKey);
    }

    [Fact]
    public async Task Handle_RetryAfterEarlierAttempt_DeletesPreviousFileThenUploadsToSameKey()
    {
        var export = Seed();
        GivenPages(AttemptRecords(1));
        export.BeginRun(TrainingExportBuilder.FileKey, Now.AddSeconds(-20), TimeSpan.FromMinutes(30));
        export.FailAttempt("OperationCanceledException", Now.AddSeconds(-10), 3, TimeSpan.FromSeconds(1));

        await Handle(export.Id, batchSize: 500);

        _events.Should().Equal("save", "delete:" + TrainingExportBuilder.FileKey, "upload:" + TrainingExportBuilder.FileKey, "save");
        (export.Status, export.FileKey, export.Attempts).Should().Be((TrainingExportStatus.Completed, TrainingExportBuilder.FileKey, 2));
    }

    [Fact]
    public async Task Handle_ClaimLostToAnotherRun_ThrowsBeforeTouchingStorage()
    {
        var export = Seed();
        GivenPages(AttemptRecords(1));
        (_failingSave, _saveFailure) = (1, new ConflictCoreException(ErrorCodes.TrainingExportModifiedConcurrently));

        var act = () => Handle(export.Id, batchSize: 500);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TrainingExportModifiedConcurrently);
        _events.Should().Equal("save");
        await _attemptRepository.DidNotReceive().GetExportPageAsync(Arg.Any<TrainingRecordFilter>(), Arg.Any<TrainingRecordCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private TrainingExport Seed()
    {
        var export = new TrainingExportBuilder().WithSource(TrainingExportSource.Attempts).ForSubject(_subjectId).Build();
        _exports.Add(export);
        return export;
    }

    private void GivenPages(params List<AttemptTrainingRecord>[] pages)
    {
        _attemptRepository.GetExportPageAsync(Arg.Any<TrainingRecordFilter>(), Arg.Any<TrainingRecordCursor?>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(pages[0], pages[1..]);
    }

    private static List<AttemptTrainingRecord> AttemptRecords(int count)
    {
        var session = new SessionBuilder().Build(count);
        return session.Items
            .Select(x => session.RecordAttempt(x, SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 1000))
            .Select(x => AttemptTrainingRecord.From(x, session.Kind, new QuestionPlacement(x.QuestionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), StudentHash, Now))
            .ToList();
    }

    private Task Handle(Guid exportId, int batchSize)
    {
        var options = Options.Create(new TrainingExportsOptions { ReadBatchSize = batchSize });
        var handler = new RunTrainingExportHandler(_trainingExportRepository, _attemptRepository, Substitute.For<IAvatarTrainingRecordRepository>(), Substitute.For<ITeacherThreadTrainingRecordRepository>(), Substitute.For<IEssayGradeTrainingRecordRepository>(), Substitute.For<IStudentIdHasher>(), _fileStorage, options, _timeProvider);
        return handler.Handle(new RunTrainingExportCommand(exportId), TestContext.Current.CancellationToken);
    }
}
