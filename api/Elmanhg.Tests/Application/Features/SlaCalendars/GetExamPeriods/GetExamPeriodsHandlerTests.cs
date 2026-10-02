using Elmanhg.Application.SlaCalendars.GetExamPeriods;
using Elmanhg.Application.SlaCalendars.Shared;
using Elmanhg.Domain.SlaCalendars;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.SlaCalendars.GetExamPeriods;

public sealed class GetExamPeriodsHandlerTests
{
    private readonly IExamPeriodRepository _examPeriodRepository = Substitute.For<IExamPeriodRepository>();

    [Fact]
    public async Task Handle_Periods_MapsAllFields()
    {
        var period = ExamPeriod.Create("Final exams", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 15), Guid.NewGuid());
        _examPeriodRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamPeriod>, IQueryable<ExamPeriod>>?>(), Arg.Any<Func<IQueryable<ExamPeriod>, IOrderedQueryable<ExamPeriod>>?>(), true).Returns([period]);

        var result = await Handle();

        result.Should().Equal(new ExamPeriodResult(period.Id, "Final exams", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 15), period.CreationDate, period.UpdationDate));
        await _examPeriodRepository.Received(1).GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamPeriod>, IQueryable<ExamPeriod>>?>(), Arg.Any<Func<IQueryable<ExamPeriod>, IOrderedQueryable<ExamPeriod>>?>(), true);
    }

    [Fact]
    public async Task Handle_RepositoryReturnsNull_ReturnsEmpty()
    {
        _examPeriodRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<ExamPeriod>, IQueryable<ExamPeriod>>?>(), Arg.Any<Func<IQueryable<ExamPeriod>, IOrderedQueryable<ExamPeriod>>?>(), Arg.Any<bool>()).Returns((List<ExamPeriod>?)null);

        var result = await Handle();

        result.Should().BeEmpty();
    }

    private Task<List<ExamPeriodResult>> Handle() => new GetExamPeriodsHandler(_examPeriodRepository).Handle(new GetExamPeriodsQuery(), TestContext.Current.CancellationToken);
}
