using Core.DDD.Models;
using Elmanhg.Application.TrainingExports.GetTrainingExports;
using Elmanhg.Domain.TrainingExports;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.TrainingExports.GetTrainingExports;

public sealed class GetTrainingExportsHandlerTests
{
    private readonly ITrainingExportRepository _trainingExportRepository = Substitute.For<ITrainingExportRepository>();

    [Fact]
    public async Task Handle_Exports_MapsPageToResults()
    {
        var export = new TrainingExportBuilder().WithSource(TrainingExportSource.EssayGrades).Completed().Build();
        _trainingExportRepository.FindPaginatedAsync(2, 5, Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<TrainingExport, bool>>?>(), Arg.Any<Func<IQueryable<TrainingExport>, IQueryable<TrainingExport>>?>(), Arg.Any<Func<IQueryable<TrainingExport>, IOrderedQueryable<TrainingExport>>?>(), true)
            .Returns(new PageData<TrainingExport> { Items = [export], PageNumber = 2, PageSize = 5, TotalItems = 6, TotalPages = 2 });
        var handler = new GetTrainingExportsHandler(_trainingExportRepository);

        var page = await handler.Handle(new GetTrainingExportsQuery(2, 5), TestContext.Current.CancellationToken);

        var result = page.Items.Should().ContainSingle().Subject;
        (result.Id, result.Source, result.Status, result.RowCount, result.FileSizeBytes, result.Sha256).Should().Be((export.Id, TrainingExportSource.EssayGrades, TrainingExportStatus.Completed, (long?)2, (long?)128, (string?)TrainingExportBuilder.Sha256));
        (result.FileName, result.CompletedAt, result.ExpiresAt).Should().Be(("elmanhg-essay-grades-2026-01-01-2026-02-01.jsonl", (DateTimeOffset?)TrainingExportBuilder.CompletedAt, (DateTimeOffset?)TrainingExportBuilder.ExpiresAt));
        (page.PageNumber, page.PageSize, page.TotalItems, page.TotalPages).Should().Be((2L, 5L, 6L, 2L));
    }
}
