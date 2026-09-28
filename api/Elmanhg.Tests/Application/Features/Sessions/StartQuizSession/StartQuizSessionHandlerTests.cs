using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.StartQuizSession;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Selection;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;
using System.Security.Claims;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Sessions.StartQuizSession;

public sealed class StartQuizSessionHandlerTests
{
    private const int Seed = 42;
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SessionBuilder _builder = new();
    private readonly List<QuestionRevision> _revisions = [];
    private readonly List<QuestionAttemptSummary> _summaries = [];
    private readonly StartQuizSessionHandler _handler;
    private List<Question> _pool;

    public StartQuizSessionHandlerTests()
    {
        _pool = Questions(2);
        _currentUserService.UserId.Returns(_builder.StudentId);
        StubLesson(_builder.Questions.Lesson);
        _questionRepository.GetServableIdsInLessonAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => _pool.Select(x => x.Id).ToList());
        _sessionRepository.GetAttemptSummariesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns(_summaries);
        StubFindQuestions(_ => true);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_revisions);
        SessionRepositoryStub.StubFind(_sessionRepository);
        _handler = new StartQuizSessionHandler(_sessionRepository, _lessonRepository, _questionRepository, Options.Create(new SessionsOptions()), Options.Create(new MasteryOptions()), new Random(Seed), _currentUserService, Substitute.For<ILocalizer>());
    }

    private Guid LessonId => _builder.Questions.Lesson.Id;

    [Fact]
    public async Task Handle_NoOpenSession_StartsQuizFromWholeSmallPool()
    {
        var result = await _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        await _sessionRepository.Received(1).AddAsync(Arg.Is<Session>(x => x.Items.Count == 2 && x.StudentId == _builder.StudentId), Arg.Any<CancellationToken>());
        result.Items.Should().HaveCount(2);
        result.Kind.Should().Be("Quiz");
        result.Items.Should().AllSatisfy(item =>
        {
            item.Type.Should().Be("Mcq");
            item.CorrectAnswer.Should().BeNull();
        });
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoQuestionCount_UsesDefaultQuizSize()
    {
        _pool = Questions(12);

        var result = await _handler.Handle(new StartQuizSessionCommand(LessonId, null), TestContext.Current.CancellationToken);

        result.Items.Should().HaveCount(10);
    }

    [Fact]
    public async Task Handle_PoolLargerThanCount_ServesRequestedCountWithoutRepeats()
    {
        _pool = Questions(8);

        var result = await _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        result.Items.Select(x => x.QuestionId).Should().HaveCount(5).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Handle_AttemptHistory_OrdersItemsByBucket()
    {
        _pool = Questions(4);
        _summaries.AddRange([new QuestionAttemptSummary(_pool[1].Id, 1, 0, T0, null), new QuestionAttemptSummary(_pool[2].Id, 1, 1, T0, T0), new QuestionAttemptSummary(_pool[3].Id, 2, 2, T0, T0)]);

        var result = await _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        result.Items.OrderBy(x => x.Position).Select(x => x.QuestionId).Should().Equal(_pool.Select(x => x.Id));
        await _sessionRepository.Received(1).GetAttemptSummariesAsync(_builder.StudentId, Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 4), 0.8m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SelectedQuestionMissingOnLoad_ServesTheRest()
    {
        _pool = Questions(3);
        var deletedId = _pool[0].Id;
        StubFindQuestions(x => x.Id != deletedId);

        var result = await _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        result.Items.Should().HaveCount(2);
        await _sessionRepository.Received(1).AddAsync(Arg.Is<Session>(x => x.Items.Count == 2 && x.Items.All(item => item.QuestionId != deletedId)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OpenSessionForLesson_ResumesIt()
    {
        var existing = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, Questions(2), isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, existing);

        var result = await _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        result.Id.Should().Be(existing.Id);
        await _sessionRepository.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _questionRepository.DidNotReceive().GetServableIdsInLessonAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _sessionRepository.DidNotReceive().GetAttemptSummariesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OpenSessionOfOtherStudent_StartsNewSession()
    {
        SessionRepositoryStub.StubFind(_sessionRepository, Session.StartQuiz(Guid.NewGuid(), _builder.Questions.Lesson, Questions(2), isTestMode: false));

        var result = await _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        await _sessionRepository.Received(1).AddAsync(Arg.Is<Session>(x => x.Id == result.Id && x.StudentId == _builder.StudentId), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AdminCaller_StartsTestModeSession()
    {
        _currentUserService.GetClaim(ClaimTypes.Role).Returns(nameof(UserRole.Admin));

        var result = await _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        result.IsTestMode.Should().BeTrue();
        await _sessionRepository.Received(1).AddAsync(Arg.Is<Session>(x => x.IsTestMode), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonMissing_ThrowsLessonNotFound()
    {
        var act = () => _handler.Handle(new StartQuizSessionCommand(Guid.NewGuid(), 5), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LessonDraft_ThrowsLessonNotFound()
    {
        var draft = new QuestionBuilder().Lesson;
        StubLesson(draft);

        var act = () => _handler.Handle(new StartQuizSessionCommand(draft.Id, 5), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.LessonNotFound);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoServableQuestions_ThrowsSessionNoServableQuestions()
    {
        _pool = [];

        var act = () => _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.SessionNoServableQuestions);
        await _sessionRepository.DidNotReceive().GetAttemptSummariesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>());
        await _sessionRepository.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private void StubLesson(Lesson lesson)
    {
        _lessonRepository.GetByIdAsync(lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(lesson);
    }

    private void StubFindQuestions(Func<Question, bool> available)
    {
        _questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>())
            .Returns(call => _pool.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).Where(available).ToList());
    }

    private List<Question> Questions(int count)
    {
        var questions = _builder.BuildQuestions(count);
        _revisions.AddRange(questions.SelectMany(x => x.Revisions));
        return questions;
    }
}
