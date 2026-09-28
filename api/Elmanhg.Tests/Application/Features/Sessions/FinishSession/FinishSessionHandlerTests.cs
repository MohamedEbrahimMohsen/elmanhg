using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Localization;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.FinishSession;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Sessions.FinishSession;

public sealed class FinishSessionHandlerTests
{
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly SessionBuilder _builder = new();
    private readonly Session _session;
    private readonly FinishSessionHandler _handler;

    public FinishSessionHandlerTests()
    {
        _currentUserService.UserId.Returns(_builder.StudentId);
        var questions = _builder.BuildQuestions(2);
        _session = Session.StartQuiz(_builder.StudentId, _builder.Questions.Lesson, questions, isTestMode: false);
        SessionRepositoryStub.StubFind(_sessionRepository, _session);
        _questionRepository.GetRevisionsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(questions.SelectMany(x => x.Revisions).ToList());
        _handler = new FinishSessionHandler(_sessionRepository, _questionRepository, _currentUserService, Substitute.For<ILocalizer>());
    }

    [Fact]
    public async Task Handle_OpenSession_SubmitsScoresAndSaves()
    {
        _session.RecordAttempt(_session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);

        var result = await _handler.Handle(new FinishSessionCommand(_session.Id), TestContext.Current.CancellationToken);

        result.SubmittedAt.Should().NotBeNull();
        result.ScorePercent.Should().Be(50.00m);
        result.CurrentPosition.Should().BeNull();
        result.Items[1].Attempt.Should().BeNull();
        result.Items[1].CorrectAnswer.Should().NotBeNull();
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadySubmitted_ReturnsSameSubmission()
    {
        _session.Submit();
        var submittedAt = _session.SubmittedAt;

        var result = await _handler.Handle(new FinishSessionCommand(_session.Id), TestContext.Current.CancellationToken);

        result.SubmittedAt.Should().Be(submittedAt);
        await _sessionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new FinishSessionCommand(_session.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OtherStudentsSession_ThrowsSessionNotFound()
    {
        _currentUserService.UserId.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new FinishSessionCommand(_session.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionNotFound);
        _session.SubmittedAt.Should().BeNull();
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExamSession_ThrowsNotFound()
    {
        var exam = Session.StartUnitExam(_builder.StudentId, _builder.Questions.Unit, ExamBlueprint.CreateForUnit(_builder.Questions.Unit, ExamBlueprintBuilder.Shape(new ExamTypeCount(QuestionType.Mcq, 1)), ExamBlueprintBuilder.Plenty(), Guid.NewGuid()), _builder.BuildQuestions(1), [_builder.Questions.Lesson], false, DateTimeOffset.UtcNow);
        SessionRepositoryStub.StubFind(_sessionRepository, exam);

        var act = () => _handler.Handle(new FinishSessionCommand(exam.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionNotFound);
        exam.SubmittedAt.Should().BeNull();
        await _sessionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
