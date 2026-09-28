using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.GetSession;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Sessions.GetSession;

public sealed class GetSessionHandlerTests
{
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SessionBuilder _builder = new();
    private readonly Session _session;
    private readonly GetSessionHandler _handler;

    public GetSessionHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        var questions = _builder.BuildQuestions(2);
        _session = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, questions, isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, _session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(questions.SelectMany(x => x.Revisions).ToList());
        _handler = new GetSessionHandler(_sessionRepository, _questionRepository, _currentUserService, Substitute.For<ILocalizer>());
    }

    [Fact]
    public async Task Handle_OwnSessionWithOneAnswer_ReturnsProgress()
    {
        _session.RecordAttempt(_session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        var result = await _handler.Handle(new GetSessionQuery(_session.Id), TestContext.Current.CancellationToken);

        result.CurrentPosition.Should().Be(2);
        var (answered, open) = (result.Items[0], result.Items[1]);
        answered.Attempt.Should().NotBeNull();
        answered.CorrectAnswer.Should().NotBeNull();
        answered.Explanation.Should().Be(QuestionBuilder.McqContent().Explanation);
        open.Attempt.Should().BeNull();
        open.CorrectAnswer.Should().BeNull();
        open.Explanation.Should().BeNull();
        open.Stem.Should().Be(QuestionBuilder.McqContent().Stem);
        open.Body.GetProperty("options").GetArrayLength().Should().Be(2);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SubmittedSession_RevealsUnansweredItems()
    {
        _session.Submit();

        var result = await _handler.Handle(new GetSessionQuery(_session.Id), TestContext.Current.CancellationToken);

        result.Items[1].CorrectAnswer.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_OtherStudentsSession_ThrowsSessionNotFound()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new GetSessionQuery(_session.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionNotFound);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetSessionQuery(_session.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }
}
