using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.Shared;
using Elmanhg.Application.Sessions.SubmitAnswer;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Application.Features.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Sessions.SubmitAnswer;

public sealed class SubmitAnswerFreeTierTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly IQuestionMasteryRepository _questionMasteryRepository = Substitute.For<IQuestionMasteryRepository>();
    private readonly IEssayGradeRepository _essayGradeRepository = Substitute.For<IEssayGradeRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SessionBuilder _builder = new();
    private readonly List<Question> _questions;
    private readonly Session _session;
    private readonly SubmitAnswerHandler _handler;

    public SubmitAnswerFreeTierTests()
    {
        var lesson = _builder.Questions.Lesson;
        _currentUserService.UserId.Returns(_builder.StudentId);
        _timeProvider.GetUtcNow().Returns(T0);
        _questions = _builder.BuildQuestions(2);
        _session = Session.StartQuiz(_builder.StudentId, lesson, _questions, isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, _session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_questions[0].Revisions);
        _lessonRepository.GetPublishedSiblingPositionsAsync(lesson.Id, Arg.Any<CancellationToken>()).Returns([LessonPosition.Of(lesson), new LessonPosition(Guid.NewGuid(), lesson.UnitId, 2, lesson.CreationDate)]);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        StubUsedToday(0);
        _handler = new SubmitAnswerHandler(_sessionRepository, _questionRepository, _questionMasteryRepository, _lessonRepository, _subscriptionRepository, _essayGradeRepository, Options.Create(new MasteryOptions()), Options.Create(new SubscriptionsOptions()), Options.Create(new ContentOptions { QuestionEssayAnswerMaxLength = 20000 }), Options.Create(new SessionsOptions()), _timeProvider, _currentUserService, Substitute.For<ILocalizer>(), Substitute.For<IAiMathCheckClient>());
    }

    [Fact]
    public async Task Handle_FreeStudentAtDailyLimit_ThrowsQuizDailyLimitReached()
    {
        StubUsedToday(10);

        var act = () => Handle(_session);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.QuizDailyLimitReached);
        _session.Attempts.Should().BeEmpty();
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FreeStudentAtDailyLimitReplayingSameAnswer_ReturnsExistingAttempt()
    {
        var existing = _session.RecordAttempt(_session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 1000);
        StubUsedToday(10);

        var result = await Handle(_session);

        result.Attempt!.Id.Should().Be(existing.Id);
        _session.Attempts.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_FreeStudentLockedLesson_ThrowsLessonLocked()
    {
        var lesson = _builder.Questions.Lesson;
        _lessonRepository.GetPublishedSiblingPositionsAsync(lesson.Id, Arg.Any<CancellationToken>()).Returns([new LessonPosition(Guid.NewGuid(), lesson.UnitId, 0, lesson.CreationDate), LessonPosition.Of(lesson)]);

        var act = () => Handle(_session);

        (await act.Should().ThrowAsync<ForbiddenCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonLocked);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FreeStudentBelowLimit_RecordsAttempt()
    {
        StubUsedToday(9);

        await Handle(_session);

        _session.Attempts.Should().ContainSingle();
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TestModeSessionAtLimit_RecordsAttempt()
    {
        var testSession = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, _questions, isTestMode: true);
        SessionRepositoryStub.StubFind(_sessionRepository, testSession);
        StubUsedToday(10);

        await Handle(testSession);

        testSession.Attempts.Should().ContainSingle();
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubscribedStudentAboveFreeLimit_RecordsAttempt()
    {
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_builder.StudentId, T0));
        StubUsedToday(50);

        await Handle(_session);

        _session.Attempts.Should().ContainSingle();
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingEssayReplay_SkipsFreeTierGate()
    {
        var essay = _builder.Questions.Essay().Approved().Build();
        var session = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, [essay], isTestMode: false);
        const string Answer = """{"text":"القصور الذاتي"}""";
        session.SubmitEssay(session.Items[0], Answer, 0);
        SessionRepositoryStub.StubFind(_sessionRepository, session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(essay.Revisions);
        StubUsedToday(10);

        var result = await _handler.Handle(new SubmitAnswerCommand(session.Id, essay.Id, QuestionBuilder.Json(Answer), 1000), TestContext.Current.CancellationToken);

        result.PendingAnswer.Should().NotBeNull();
        await _essayGradeRepository.DidNotReceive().AddAsync(Arg.Any<EssayGrade>(), Arg.Any<CancellationToken>());
    }

    private Task<SessionItemResult> Handle(Session session) => _handler.Handle(new SubmitAnswerCommand(session.Id, _questions[0].Id, QuestionBuilder.Json(SessionBuilder.AnswerB), 1000), TestContext.Current.CancellationToken);

    private void StubUsedToday(int used) => _sessionRepository.CountQuizAttemptsOnDayAsync(_builder.StudentId, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(used);
}
