using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Sessions.StartQuizSession;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Storage;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Selection;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;

namespace Elmanhg.Tests.Application.Features.Sessions.StartQuizSession;

public sealed class StartQuizSessionFreeTierTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SessionBuilder _builder = new();
    private readonly List<Question> _pool;
    private readonly StartQuizSessionHandler _handler;

    public StartQuizSessionFreeTierTests()
    {
        _pool = _builder.BuildQuestions(2);
        var lesson = _builder.Questions.Lesson;
        _currentUserService.UserId.Returns(_builder.StudentId);
        _timeProvider.GetUtcNow().Returns(T0);
        _lessonRepository.GetByIdAsync(lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(lesson);
        _lessonRepository.GetPublishedSiblingPositionsAsync(lesson.Id, Arg.Any<CancellationToken>()).Returns([LessonPosition.Of(lesson), new LessonPosition(Guid.NewGuid(), lesson.UnitId, 2, lesson.CreationDate)]);
        _questionRepository.GetServableIdsInLessonAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => _pool.Select(x => x.Id).ToList());
        _sessionRepository.GetAttemptSummariesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns(new List<QuestionAttemptSummary>());
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => _pool.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).ToList());
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_ => _pool.SelectMany(x => x.Revisions).ToList());
        SessionRepositoryStub.StubFind(_sessionRepository);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        StubUsedToday(0);
        _handler = new StartQuizSessionHandler(_sessionRepository, _lessonRepository, _questionRepository, _subscriptionRepository, Options.Create(new SessionsOptions()), Options.Create(new MasteryOptions()), Options.Create(new SubscriptionsOptions()), new Random(42), _timeProvider, _currentUserService, Substitute.For<ILocalizer>(), Substitute.For<IFileStorage>());
    }

    private Guid LessonId => _builder.Questions.Lesson.Id;

    [Fact]
    public async Task Handle_FreeStudentLockedLesson_ThrowsLessonLocked()
    {
        LockSessionLesson();

        var act = () => Handle();

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonLocked);
        await _sessionRepository.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FreeStudentAtDailyLimit_ThrowsQuizDailyLimitReached()
    {
        StubUsedToday(10);

        var act = () => Handle();

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuizDailyLimitReached);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FreeStudentAtDailyLimitWithOpenSession_ResumesSession()
    {
        StubUsedToday(10);
        var open = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, _pool, isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, open);

        var result = await Handle();

        result.Id.Should().Be(open.Id);
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FreeStudentBelowLimitOnOpenLesson_StartsQuiz()
    {
        StubUsedToday(9);

        var result = await Handle();

        result.IsTestMode.Should().BeFalse();
        await _sessionRepository.Received(1).AddAsync(Arg.Is<Session>(x => x.Id == result.Id), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdminOnLockedLessonAtLimit_StartsTestModeQuiz()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));
        LockSessionLesson();
        StubUsedToday(10);

        var result = await Handle();

        result.IsTestMode.Should().BeTrue();
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EntitlementLookupFails_ThrowsAndSavesNothing()
    {
        _subscriptionRepository.FindAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>())
            .Returns<List<Subscription>>(_ => throw new InvalidOperationException("Subscriptions unavailable."));

        var act = () => Handle();

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task<SessionResult> Handle() => _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

    private void LockSessionLesson()
    {
        var lesson = _builder.Questions.Lesson;
        _lessonRepository.GetPublishedSiblingPositionsAsync(lesson.Id, Arg.Any<CancellationToken>()).Returns([new LessonPosition(Guid.NewGuid(), lesson.UnitId, 0, lesson.CreationDate), LessonPosition.Of(lesson)]);
    }

    private void StubUsedToday(int used) => _sessionRepository.CountQuizAttemptsOnDayAsync(_builder.StudentId, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(used);
}
