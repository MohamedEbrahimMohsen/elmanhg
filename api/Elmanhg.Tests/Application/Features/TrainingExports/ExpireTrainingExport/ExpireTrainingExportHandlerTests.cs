using Core.Storage;
using Elmanhg.Application.TrainingExports.ExpireTrainingExport;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TrainingExports.ExpireTrainingExport;

public sealed class ExpireTrainingExportHandlerTests
{
    private readonly ITrainingExportRepository _trainingExportRepository = Substitute.For<ITrainingExportRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<TrainingExport> _exports = [];

    public ExpireTrainingExportHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(TrainingExportBuilder.ExpiresAt);
        _trainingExportRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TrainingExport, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TrainingExport>, IQueryable<TrainingExport>>?>(), Arg.Any<Func<IQueryable<TrainingExport>, IOrderedQueryable<TrainingExport>>?>(), Arg.Any<bool>())
            .Returns(call => _exports.FirstOrDefault(call.Arg<Expression<Func<TrainingExport, bool>>>().Compile()));
    }

    [Fact]
    public async Task Handle_ExpiredExport_DeletesFileAndMarksExpired()
    {
        var export = Seed(new TrainingExportBuilder().Completed().Build());

        await Handle(export.Id);

        await _fileStorage.Received(1).DeleteAsync(TrainingExportBuilder.FileKey, Arg.Any<CancellationToken>());
        (export.Status, export.FileKey).Should().Be((TrainingExportStatus.Expired, (string?)null));
        await _trainingExportRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BeforeExpiry_DoesNothing()
    {
        var export = Seed(new TrainingExportBuilder().Completed().Build());
        _timeProvider.GetUtcNow().Returns(TrainingExportBuilder.ExpiresAt.AddTicks(-10));

        await Handle(export.Id);

        export.Status.Should().Be(TrainingExportStatus.Completed);
        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _trainingExportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownExport_DoesNothing()
    {
        await Handle(Guid.NewGuid());

        await _fileStorage.DidNotReceive().DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _trainingExportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StorageDeleteFails_PropagatesAndKeepsExportCompleted()
    {
        var export = Seed(new TrainingExportBuilder().Completed().Build());
        _fileStorage.DeleteAsync(TrainingExportBuilder.FileKey, Arg.Any<CancellationToken>()).Returns(Task.FromException(new IOException("disk")));

        var act = () => Handle(export.Id);

        await act.Should().ThrowAsync<IOException>();
        export.Status.Should().Be(TrainingExportStatus.Completed);
        await _trainingExportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedExportWithLeftoverFile_DeletesFileAndClearsKey()
    {
        var export = new TrainingExportBuilder().Build();
        export.BeginRun(TrainingExportBuilder.FileKey, TrainingExportBuilder.DefaultRequestedAt, TimeSpan.FromMinutes(30));
        export.FailAttempt("IOException", TrainingExportBuilder.DefaultRequestedAt, 1, TimeSpan.FromSeconds(60));
        Seed(export);

        await Handle(export.Id);

        await _fileStorage.Received(1).DeleteAsync(TrainingExportBuilder.FileKey, Arg.Any<CancellationToken>());
        (export.Status, export.FileKey).Should().Be((TrainingExportStatus.Failed, (string?)null));
        await _trainingExportRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private TrainingExport Seed(TrainingExport export)
    {
        _exports.Add(export);
        return export;
    }

    private Task Handle(Guid exportId) => new ExpireTrainingExportHandler(_trainingExportRepository, _fileStorage, _timeProvider).Handle(new ExpireTrainingExportCommand(exportId), TestContext.Current.CancellationToken);
}
