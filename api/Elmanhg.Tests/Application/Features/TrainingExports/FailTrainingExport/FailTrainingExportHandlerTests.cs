using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TrainingExports.FailTrainingExport;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TrainingExports.FailTrainingExport;

public sealed class FailTrainingExportHandlerTests
{
    private static readonly DateTimeOffset Now = TrainingExportBuilder.DefaultRequestedAt.AddMinutes(1);
    private readonly ITrainingExportRepository _trainingExportRepository = Substitute.For<ITrainingExportRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<TrainingExport> _exports = [];

    public FailTrainingExportHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _trainingExportRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<TrainingExport, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<TrainingExport>, IQueryable<TrainingExport>>?>(), Arg.Any<Func<IQueryable<TrainingExport>, IOrderedQueryable<TrainingExport>>?>(), Arg.Any<bool>())
            .Returns(call => _exports.FirstOrDefault(call.Arg<Expression<Func<TrainingExport, bool>>>().Compile()));
    }

    [Fact]
    public async Task Handle_PendingExport_RecordsFailureAndSaves()
    {
        var export = Seed(new TrainingExportBuilder().Build());

        await Handle(export.Id);

        (export.Attempts, export.NextAttemptAt, export.LastErrorCode, export.Status).Should().Be((1, (DateTimeOffset?)Now.AddSeconds(60), "InvalidOperationException", TrainingExportStatus.Pending));
        await _trainingExportRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CompletedExport_DoesNothing()
    {
        var export = Seed(new TrainingExportBuilder().Completed().Build());

        await Handle(export.Id);

        (export.Status, export.LastErrorCode).Should().Be((TrainingExportStatus.Completed, (string?)null));
        await _trainingExportRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private TrainingExport Seed(TrainingExport export)
    {
        _exports.Add(export);
        return export;
    }

    private Task Handle(Guid exportId) => new FailTrainingExportHandler(_trainingExportRepository, Options.Create(new TrainingExportsOptions()), _timeProvider).Handle(new FailTrainingExportCommand(exportId, "InvalidOperationException"), TestContext.Current.CancellationToken);
}
