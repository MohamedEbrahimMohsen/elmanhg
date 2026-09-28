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
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Sessions.StartQuizSession;

public sealed class StartQuizSessionHandlerTests
{
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly ILessonRepository _lessonRepository = Substitute.For<ILessonRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SessionBuilder _builder = new();
    private readonly List<QuestionRevision> _revisions = [];
    private readonly StartQuizSessionHandler _handler;

    public StartQuizSessionHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        StubLesson(_builder.Questions.Lesson);
        _questionRepository.GetRandomServableInLessonAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Questions(2));
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(_revisions);
        SessionRepositoryStub.StubFind(_sessionRepository);
        _handler = new StartQuizSessionHandler(_sessionRepository, _lessonRepository, _questionRepository, Options.Create(new SessionsOptions()), _currentUserService, Substitute.For<ILocalizer>());
    }

    private Guid LessonId => _builder.Questions.Lesson.Id;

    [Fact]
    public async Task Handle_NoOpenSession_StartsQuizWithRequestedCount()
    {
        var result = await _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        await _questionRepository.Received(1).GetRandomServableInLessonAsync(LessonId, 5, Arg.Any<CancellationToken>());
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
        await _handler.Handle(new StartQuizSessionCommand(LessonId, null), TestContext.Current.CancellationToken);

        await _questionRepository.Received(1).GetRandomServableInLessonAsync(LessonId, 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OpenSessionForLesson_ResumesIt()
    {
        var existing = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, Questions(2), isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, existing);

        var result = await _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        result.Id.Should().Be(existing.Id);
        await _sessionRepository.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _questionRepository.DidNotReceive().GetRandomServableInLessonAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
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
        _questionRepository.GetRandomServableInLessonAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<Question>());

        var act = () => _handler.Handle(new StartQuizSessionCommand(LessonId, 5), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.SessionNoServableQuestions);
        await _sessionRepository.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private void StubLesson(Lesson lesson)
    {
        _lessonRepository.GetByIdAsync(lesson.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Lesson>, IQueryable<Lesson>>?>(), Arg.Any<bool>()).Returns(lesson);
    }

    private List<Question> Questions(int count)
    {
        var questions = _builder.BuildQuestions(count);
        _revisions.AddRange(questions.SelectMany(x => x.Revisions));
        return questions;
    }
}
