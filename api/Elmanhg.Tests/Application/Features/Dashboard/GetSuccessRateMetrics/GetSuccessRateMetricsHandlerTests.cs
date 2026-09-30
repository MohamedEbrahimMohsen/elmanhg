using Core.Errors;
using Elmanhg.Application.Dashboard.GetSuccessRateMetrics;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetSuccessRateMetrics;

public sealed class GetSuccessRateMetricsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ICurriculumUnitRepository _unitRepository = Substitute.For<ICurriculumUnitRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly CurriculumUnit _unit;
    private readonly Lesson _firstLesson;
    private readonly Lesson _secondLesson;
    private readonly GetSuccessRateMetricsHandler _handler;

    public GetSuccessRateMetricsHandlerTests()
    {
        _unit = CurriculumUnit.Create(_subject, "Motion", 1, Guid.NewGuid());
        _firstLesson = Lesson.Create(_unit, "Speed", 1, Guid.NewGuid());
        _secondLesson = Lesson.Create(_unit, "Acceleration", 2, Guid.NewGuid());
        _timeProvider.GetUtcNow().Returns(Now);
        _sessionRepository.GetAttemptOutcomesByLessonAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns([]);
        _subjectRepository.FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns([_subject]);
        _unitRepository.FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>()).Returns([_unit]);
        _lessonRepository.FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns([_secondLesson, _firstLesson]);
        _handler = new GetSuccessRateMetricsHandler(_sessionRepository, _subjectRepository, _unitRepository, _lessonRepository, _timeProvider, Options.Create(new DashboardOptions()), Options.Create(new MasteryOptions { CorrectThreshold = 0.8m }));
    }

    [Fact]
    public async Task Handle_UnknownSubject_ThrowsSubjectNotFound()
    {
        var act = () => _handler.Handle(new GetSuccessRateMetricsQuery(null, null, Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubjectNotFound);
        await _sessionRepository.DidNotReceive().GetAttemptOutcomesByLessonAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RollsLessonsUpToUnitsSubjectsAndOverall()
    {
        ReturnOutcomes(Outcome(_firstLesson, 10, 6), Outcome(_secondLesson, 10, 2));

        var result = await _handler.Handle(new GetSuccessRateMetricsQuery(null, null, null), TestContext.Current.CancellationToken);

        result.Attempts.Should().Be(20);
        result.Correct.Should().Be(8);
        result.Rate.Should().Be(0.4m);
        result.BySubject.Should().Equal(new SuccessRateGroupResult(_subject.Id, "Physics", null, 20, 8, 0.4m));
        result.ByUnit.Should().Equal(new SuccessRateGroupResult(_unit.Id, "Motion", _subject.Id, 20, 8, 0.4m));
        result.ByLesson.Should().Equal(new SuccessRateGroupResult(_firstLesson.Id, "Speed", _unit.Id, 10, 6, 0.6m), new SuccessRateGroupResult(_secondLesson.Id, "Acceleration", _unit.Id, 10, 2, 0.2m));
    }

    [Fact]
    public async Task Handle_OrdersGroupsByCurriculumOrder()
    {
        ReturnOutcomes(Outcome(_secondLesson, 4, 1), Outcome(_firstLesson, 4, 3));

        var result = await _handler.Handle(new GetSuccessRateMetricsQuery(null, null, null), TestContext.Current.CancellationToken);

        result.ByLesson.Select(x => x.Id).Should().Equal(_firstLesson.Id, _secondLesson.Id);
    }

    [Fact]
    public async Task Handle_PassesMasteryCorrectThreshold()
    {
        await _handler.Handle(new GetSuccessRateMetricsQuery(null, null, null), TestContext.Current.CancellationToken);

        await _sessionRepository.Received(1).GetAttemptOutcomesByLessonAsync(new DateTimeOffset(2025, 12, 16, 22, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 15, 22, 0, 0, TimeSpan.Zero), null, 0.8m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoAttempts_ReturnsNullRateAndEmptyGroups()
    {
        var result = await _handler.Handle(new GetSuccessRateMetricsQuery(null, null, null), TestContext.Current.CancellationToken);

        result.Rate.Should().BeNull();
        result.BySubject.Should().BeEmpty();
        result.ByUnit.Should().BeEmpty();
        result.ByLesson.Should().BeEmpty();
        result.GeneratedAt.Should().Be(Now);
        await _lessonRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<Lesson, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<Func<IQueryable<Lesson>, IOrderedQueryable<Lesson>>?>(), Arg.Any<bool>());
        await _unitRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<CurriculumUnit, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IQueryable<CurriculumUnit>>?>(), Arg.Any<Func<IQueryable<CurriculumUnit>, IOrderedQueryable<CurriculumUnit>>?>(), Arg.Any<bool>());
        await _subjectRepository.DidNotReceive().FindAsync(Arg.Any<Expression<Func<Subject, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>());
    }

    private LessonAttemptOutcome Outcome(Lesson lesson, int attempts, int correct) => new(_subject.Id, _unit.Id, lesson.Id, attempts, correct);

    private void ReturnOutcomes(params LessonAttemptOutcome[] outcomes)
    {
        _sessionRepository.GetAttemptOutcomesByLessonAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid?>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns(outcomes.ToList());
    }
}
