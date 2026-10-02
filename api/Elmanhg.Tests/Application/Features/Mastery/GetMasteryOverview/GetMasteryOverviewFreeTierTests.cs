using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Mastery.GetMasteryOverview;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Domain.Units;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Fixtures.RuntimeSettings;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Mastery.GetMasteryOverview;

public sealed class GetMasteryOverviewFreeTierTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ISubjectRepository _subjectRepository = Substitute.For<ISubjectRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Subject _subject = Subject.Create("Physics", 1, Guid.NewGuid());
    private readonly Lesson _first;
    private readonly Lesson _second;
    private readonly GetMasteryOverviewHandler _handler;

    public GetMasteryOverviewFreeTierTests()
    {
        var unit = CurriculumUnit.Create(_subject, "Mechanics", 1, Guid.NewGuid());
        _first = Published(unit, "Forces", 1);
        _second = Published(unit, "Energy", 2);
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _subjectRepository.GetAllAsync(Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subject>, IQueryable<Subject>>?>(), Arg.Any<Func<IQueryable<Subject>, IOrderedQueryable<Subject>>?>(), Arg.Any<bool>()).Returns([_subject]);
        _lessonRepository.GetPublishedPositionsAsync(Arg.Any<CancellationToken>()).Returns([LessonPosition.Of(_first), LessonPosition.Of(_second)]);
        _sessionRepository.GetQuizActivityDaysAsync(_studentId, Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(new List<DateOnly>());
        _questionMasteryRepository.GetLessonCountsAsync(_studentId, null, Arg.Any<CancellationToken>()).Returns([Count(unit, _first, 10, 5), Count(unit, _second, 10, 1)]);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        _handler = new GetMasteryOverviewHandler(_questionMasteryRepository, _sessionRepository, _subjectRepository, _lessonRepository, _userRepository, _subscriptionRepository, Options.Create(new ProgressOptions()), Options.Create(new SubscriptionsOptions()), _timeProvider, _currentUserService, new FakeRuntimeSettings());
    }

    [Fact]
    public async Task Handle_FreeStudent_NextLessonSkipsLockedLesson()
    {
        var result = await _handler.Handle(new GetMasteryOverviewQuery(), TestContext.Current.CancellationToken);

        (result.NextLesson!.LessonId, result.NextLesson.LessonName).Should().Be((_first.Id, "Forces"));
    }

    [Fact]
    public async Task Handle_SubscribedStudent_NextLessonIsLowestMasteryLesson()
    {
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_studentId, Now));

        var result = await _handler.Handle(new GetMasteryOverviewQuery(), TestContext.Current.CancellationToken);

        (result.NextLesson!.LessonId, result.NextLesson.LessonName).Should().Be((_second.Id, "Energy"));
    }

    private Lesson Published(CurriculumUnit unit, string name, int order)
    {
        var lesson = Lesson.Create(unit, name, order, Guid.NewGuid());
        lesson.Publish(Guid.NewGuid());
        _lessonRepository.GetByIdAsync(lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(lesson);
        return lesson;
    }

    private LessonMasteryCount Count(CurriculumUnit unit, Lesson lesson, int servable, int mastered) => new(_subject.Id, 1, unit.Id, 1, lesson.Id, lesson.Order, servable, mastered, mastered);
}
