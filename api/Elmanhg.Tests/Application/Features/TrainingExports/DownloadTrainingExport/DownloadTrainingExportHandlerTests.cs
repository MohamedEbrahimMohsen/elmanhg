using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Application.TrainingExports.DownloadTrainingExport;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.TrainingExports.DownloadTrainingExport;

public sealed class DownloadTrainingExportHandlerTests
{
    private readonly ITrainingExportRepository _trainingExportRepository = Substitute.For<ITrainingExportRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly DownloadTrainingExportHandler _handler;

    public DownloadTrainingExportHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(TrainingExportBuilder.CompletedAt.AddHours(1));
        _handler = new DownloadTrainingExportHandler(_trainingExportRepository, _fileStorage, _timeProvider);
    }

    [Fact]
    public async Task Handle_CompletedExport_ReturnsStoredFileWithName()
    {
        var export = GivenExport(new TrainingExportBuilder().WithSource(TrainingExportSource.Attempts).Completed().Build());
        var content = new MemoryStream([1, 2, 3]);
        _fileStorage.OpenReadAsync(TrainingExportBuilder.FileKey, Arg.Any<CancellationToken>()).Returns(new StoredFile(content, 3, "application/x-ndjson"));

        var result = await _handler.Handle(new DownloadTrainingExportQuery(export.Id), TestContext.Current.CancellationToken);

        (result.Content, result.ContentType, result.FileName).Should().Be((content, "application/x-ndjson", "elmanhg-attempts-2026-01-01-2026-02-01.jsonl"));
    }

    [Fact]
    public async Task Handle_UnknownExport_ThrowsNotFound()
    {
        var act = () => _handler.Handle(new DownloadTrainingExportQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TrainingExportNotFound);
        await _fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingExport_ThrowsConflictNotReady()
    {
        var export = GivenExport(new TrainingExportBuilder().Build());

        var act = () => _handler.Handle(new DownloadTrainingExportQuery(export.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.TrainingExportNotReady);
        await _fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PastRetention_ThrowsConflictExpired()
    {
        var export = GivenExport(new TrainingExportBuilder().Completed().Build());
        _timeProvider.GetUtcNow().Returns(TrainingExportBuilder.ExpiresAt);

        var act = () => _handler.Handle(new DownloadTrainingExportQuery(export.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.TrainingExportExpired);
        await _fileStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FileMissingInStorage_ThrowsNotFound()
    {
        var export = GivenExport(new TrainingExportBuilder().Completed().Build());
        _fileStorage.OpenReadAsync(TrainingExportBuilder.FileKey, Arg.Any<CancellationToken>()).Returns((StoredFile?)null);

        var act = () => _handler.Handle(new DownloadTrainingExportQuery(export.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.TrainingExportNotFound);
    }

    private TrainingExport GivenExport(TrainingExport export)
    {
        _trainingExportRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TrainingExport, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TrainingExport>, IQueryable<TrainingExport>>?>(), Arg.Any<Func<IQueryable<TrainingExport>, IOrderedQueryable<TrainingExport>>?>(), true)
            .Returns(call => call.Arg<Expression<Func<TrainingExport, bool>>>().Compile()(export) ? export : null);
        return export;
    }
}
