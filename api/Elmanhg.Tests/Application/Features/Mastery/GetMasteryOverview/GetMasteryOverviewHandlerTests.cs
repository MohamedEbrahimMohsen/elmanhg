using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Mastery.GetMasteryOverview;
using Elmanhg.Application.Mastery.Shared;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Mastery.GetMasteryOverview;

public sealed class GetMasteryOverviewHandlerTests
{
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Subject _subjectA = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly Subject _subjectB = Subject.Create("Chemistry", 2, Guid.NewGuid());
    private readonly Lesson _lesson;
    private readonly GetMasteryOverviewHandler _handler;

    public GetMasteryOverviewHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
        _lesson = Lesson.Create(CurriculumUnit.Create(_subjectA, "Mechanics", 1, Guid.NewGuid()), "Newton's laws", 1, Guid.NewGuid());
        _lesson.Publish(Guid.NewGuid());
        _subjectRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns([_subjectA, _subjectB]);
        _lessonRepository.GetByIdAsync(_lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(_lesson);
        StubActivity([]);
        StubCounts([]);
        _handler = new GetMasteryOverviewHandler(_questionMasteryRepository, _sessionRepository, _subjectRepository, _lessonRepository, Options.Create(new ProgressOptions()), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetMasteryOverviewQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_LessonCounts_ReturnsHeadlineFromServableMasteredAndSeen()
    {
        StubCounts([Count(_subjectA, Guid.NewGuid(), 10, 4, 6), Count(_subjectA, Guid.NewGuid(), 5, 1, 2)]);

        var result = await _handler.Handle(new GetMasteryOverviewQuery(), TestContext.Current.CancellationToken);

        result.Headline.Should().Be(new MasteryHeadlineResult(15, 5, 10, 8));
    }

    [Fact]
    public async Task Handle_Subjects_ReturnsEverySubjectInOrderWithWeightedMastery()
    {
        StubCounts([Count(_subjectA, Guid.NewGuid(), 10, 5, 5), Count(_subjectA, Guid.NewGuid(), 30, 0, 0)]);

        var result = await _handler.Handle(new GetMasteryOverviewQuery(), TestContext.Current.CancellationToken);

        result.Subjects.Should().Equal(new SubjectMasteryResult(_subjectA.Id, "Physics", 40, 5, 5, 12), new SubjectMasteryResult(_subjectB.Id, "Chemistry", 0, 0, 0, 0));
    }

    [Fact]
    public async Task Handle_LessonToImprove_ReturnsNextLessonWithNames()
    {
        StubCounts([Count(_subjectA, Guid.NewGuid(), 10, 5, 5), Count(_subjectA, _lesson.Id, 10, 1, 2)]);

        var result = await _handler.Handle(new GetMasteryOverviewQuery(), TestContext.Current.CancellationToken);

        result.NextLesson.Should().Be(new NextLessonResult(_lesson.Id, "Newton's laws", _subjectA.Id, "Physics", 10));
    }

    [Fact]
    public async Task Handle_EverythingMastered_ReturnsNoNextLesson()
    {
        StubCounts([Count(_subjectA, _lesson.Id, 4, 4, 4)]);

        var result = await _handler.Handle(new GetMasteryOverviewQuery(), TestContext.Current.CancellationToken);

        result.NextLesson.Should().BeNull();
        await _lessonRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Handle_ActivityDays_CountsStreakInConfiguredTimeZone()
    {
        _timeProvider.GetUtcNow().Returns(new DateTimeOffset(2026, 9, 27, 22, 30, 0, TimeSpan.Zero));
        StubActivity([new DateOnly(2026, 9, 28)]);

        var result = await _handler.Handle(new GetMasteryOverviewQuery(), TestContext.Current.CancellationToken);

        result.StreakDays.Should().Be(1);
    }

    private static LessonMasteryCount Count(Subject subject, Guid lessonId, int servable, int mastered, int seen) => new(subject.Id, subject.Order, Guid.NewGuid(), 1, lessonId, 1, servable, mastered, seen);

    private void StubCounts(List<LessonMasteryCount> counts) => _questionMasteryRepository.GetLessonCountsAsync(_studentId, null, Arg.Any<CancellationToken>()).Returns(counts);

    private void StubActivity(List<DateOnly> days) => _sessionRepository.GetQuizActivityDaysAsync(_studentId, "Africa/Cairo", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(days);
}
